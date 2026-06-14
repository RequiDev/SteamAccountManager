using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SteamAccountManager.App.Models;
using SteamAccountManager.Core.Steam;

namespace SteamAccountManager.App.Services;

/// <summary>
/// Orchestrates "add account": starts a real Steam login, then polls the account list until a
/// previously-unknown SteamID appears (the user finished logging in) or the timeout elapses.
/// </summary>
public interface IAccountAddCoordinator
{
    Task<AccountListItem?> BeginAddAndWaitAsync(TimeSpan pollInterval, TimeSpan timeout, CancellationToken ct);
}

public sealed class AccountAddCoordinator : IAccountAddCoordinator
{
    private readonly IAccountSwitcher _switcher;
    private readonly IAccountListService _accounts;

    public AccountAddCoordinator(IAccountSwitcher switcher, IAccountListService accounts)
    {
        _switcher = switcher;
        _accounts = accounts;
    }

    /// <summary>Pure diff helper: accounts present in <paramref name="after"/> but not in <paramref name="before"/>.</summary>
    public static IReadOnlyList<AccountListItem> FindNewAccounts(
        IReadOnlyList<AccountListItem> before,
        IReadOnlyList<AccountListItem> after)
    {
        var known = before.Select(a => a.SteamId64).ToHashSet(StringComparer.Ordinal);
        return after.Where(a => !known.Contains(a.SteamId64)).ToList();
    }

    public async Task<AccountListItem?> BeginAddAndWaitAsync(
        TimeSpan pollInterval, TimeSpan timeout, CancellationToken ct)
    {
        if (ct.IsCancellationRequested)
        {
            return null;
        }

        var before = _accounts.GetAccounts();

        try
        {
            await Task.Run(() => _switcher.BeginAddAccount(), ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return null;
        }

        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (ct.IsCancellationRequested)
            {
                return null;
            }

            var current = _accounts.GetAccounts();
            var added = FindNewAccounts(before, current);
            if (added.Count > 0)
            {
                return added[0];
            }

            try
            {
                await Task.Delay(pollInterval, ct).ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {
                return null;
            }
        }

        return null;
    }
}
