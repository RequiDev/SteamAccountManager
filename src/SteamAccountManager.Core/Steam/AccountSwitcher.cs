using System;
using System.Linq;
using SteamAccountManager.Core.Models;

namespace SteamAccountManager.Core.Steam;

public interface IAccountSwitcher
{
    void SwitchTo(string steamId64);
    void BeginAddAccount();

    /// <summary>
    /// Opportunistically records the currently-cached account tokens from local.vdf (read-only).
    /// Safe to call while Steam is running; keeps the token store current between switches.
    /// </summary>
    void CaptureTokens();
}

public sealed class AccountSwitcher : IAccountSwitcher
{
    private static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(30);

    private readonly ISteamLocator _locator;
    private readonly ILoginUsersStore _loginUsers;
    private readonly ISteamRegistry _registry;
    private readonly ISteamProcessController _process;
    private readonly IBackupService _backup;
    private readonly IConnectCacheStore _connectCache;

    public AccountSwitcher(
        ISteamLocator locator,
        ILoginUsersStore loginUsers,
        ISteamRegistry registry,
        ISteamProcessController process,
        IBackupService backup,
        IConnectCacheStore connectCache)
    {
        _locator = locator;
        _loginUsers = loginUsers;
        _registry = registry;
        _process = process;
        _backup = backup;
        _connectCache = connectCache;
    }

    public void SwitchTo(string steamId64)
    {
        var paths = _locator.Locate() ?? throw new SteamNotFoundException();

        var target = _loginUsers.Read(paths.LoginUsersPath)
            .FirstOrDefault(a => a.SteamId64 == steamId64)
            ?? throw new AccountNotFoundException(steamId64);

        EnsureSteamClosed();

        // Preserve every account's token: capture local.vdf's current ConnectCache entries and
        // re-inject the full union, so a token Steam pruned for the target is restored and it can
        // silently auto-login. Best-effort — token preservation must never block the switch.
        TryPreserveTokens(paths.LocalVdfPath);

        var previousAutoLogin = _registry.GetAutoLoginUser();
        var previousRemember = _registry.GetRememberPassword();
        _backup.Backup(paths.LoginUsersPath);

        try
        {
            _registry.SetAutoLoginUser(target.AccountName);
            _registry.SetRememberPassword(true);
            _loginUsers.SetActiveAccount(paths.LoginUsersPath, steamId64);
        }
        catch
        {
            _backup.Restore(paths.LoginUsersPath);
            if (previousAutoLogin is not null)
            {
                _registry.SetAutoLoginUser(previousAutoLogin);
            }
            else
            {
                // No auto-login user existed before; leave it cleared rather than
                // stranding the target account name (symmetric rollback).
                _registry.ClearAutoLoginUser();
            }

            _registry.SetRememberPassword(previousRemember);
            throw;
        }

        _process.Launch();
    }

    public void BeginAddAccount()
    {
        EnsureSteamClosed();
        _registry.ClearAutoLoginUser();
        _process.Launch();
    }

    public void CaptureTokens()
    {
        var paths = _locator.Locate();
        if (paths is null)
        {
            return;
        }

        try
        {
            _connectCache.Capture(paths.LocalVdfPath);
        }
        catch
        {
            // best-effort
        }
    }

    private void TryPreserveTokens(string localVdfPath)
    {
        try
        {
            _connectCache.Merge(localVdfPath);
        }
        catch
        {
            // best-effort
        }
    }

    private void EnsureSteamClosed()
    {
        if (_process.IsSteamRunning() && !_process.ShutdownAndWait(ShutdownTimeout))
        {
            throw new SteamShutdownException();
        }
    }
}
