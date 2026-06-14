using System;
using System.Linq;
using SteamAccountManager.Core.Models;

namespace SteamAccountManager.Core.Steam;

public interface IAccountSwitcher
{
    void SwitchTo(string steamId64);
    void BeginAddAccount();
}

public sealed class AccountSwitcher : IAccountSwitcher
{
    private static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(30);

    private readonly ISteamLocator _locator;
    private readonly ILoginUsersStore _loginUsers;
    private readonly ISteamRegistry _registry;
    private readonly ISteamProcessController _process;
    private readonly IBackupService _backup;
    private readonly ISteamTokenStore _tokenStore;

    public AccountSwitcher(
        ISteamLocator locator,
        ILoginUsersStore loginUsers,
        ISteamRegistry registry,
        ISteamProcessController process,
        IBackupService backup,
        ISteamTokenStore tokenStore)
    {
        _locator = locator;
        _loginUsers = loginUsers;
        _registry = registry;
        _process = process;
        _backup = backup;
        _tokenStore = tokenStore;
    }

    public void SwitchTo(string steamId64)
    {
        var paths = _locator.Locate() ?? throw new SteamNotFoundException();

        var target = _loginUsers.Read(paths.LoginUsersPath)
            .FirstOrDefault(a => a.SteamId64 == steamId64)
            ?? throw new AccountNotFoundException(steamId64);

        // Which account is logged in right now (its token is the one currently in local.vdf).
        var current = _registry.GetActiveAccountSteamId64();

        EnsureSteamClosed();

        // Save the outgoing account's token so switching back to it later is silent.
        if (current is not null)
        {
            _tokenStore.Capture(current, paths.LocalVdfPath);
        }

        var previousAutoLogin = _registry.GetAutoLoginUser();
        var previousRemember = _registry.GetRememberPassword();
        _backup.Backup(paths.LoginUsersPath);

        try
        {
            _registry.SetAutoLoginUser(target.AccountName);
            _registry.SetRememberPassword(true);
            _loginUsers.SetActiveAccount(paths.LoginUsersPath, steamId64);

            // Put the target's saved token in place LAST, after the throwable writes — so a
            // failed switch never leaves the live local.vdf out of sync with rolled-back
            // selectors. Best-effort: no saved token -> Steam shows the login screen this once,
            // and we capture it on the next switch away.
            _tokenStore.Restore(steamId64, paths.LocalVdfPath);
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

    private void EnsureSteamClosed()
    {
        if (_process.IsSteamRunning() && !_process.ShutdownAndWait(ShutdownTimeout))
        {
            throw new SteamShutdownException();
        }
    }
}
