using System.Collections.Generic;
using SteamAccountManager.App.Models;
using SteamAccountManager.Core.Steam;
using SteamAccountManager.Core.Storage;

namespace SteamAccountManager.App.Services;

/// <summary>Reads accounts from Steam and joins them with app metadata into UI items.</summary>
public interface IAccountListService
{
    IReadOnlyList<AccountListItem> GetAccounts();
}

public sealed class AccountListService : IAccountListService
{
    private readonly ISteamLocator _locator;
    private readonly ILoginUsersStore _loginUsers;
    private readonly IAccountMetadataStore _metadata;

    public AccountListService(
        ISteamLocator locator,
        ILoginUsersStore loginUsers,
        IAccountMetadataStore metadata)
    {
        _locator = locator;
        _loginUsers = loginUsers;
        _metadata = metadata;
    }

    public IReadOnlyList<AccountListItem> GetAccounts()
    {
        var paths = _locator.Locate();
        if (paths is null)
        {
            return new List<AccountListItem>();
        }

        var accounts = _loginUsers.Read(paths.LoginUsersPath);
        var result = new List<AccountListItem>(accounts.Count);
        foreach (var account in accounts)
        {
            // IAccountMetadataStore.Get never returns null (returns a fresh default when absent).
            var meta = _metadata.Get(account.SteamId64);
            result.Add(AccountListItem.From(account, meta));
        }

        return result;
    }
}
