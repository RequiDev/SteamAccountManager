using System;
using System.Collections.Generic;
using SteamAccountManager.App.Models;
using SteamAccountManager.App.Services;

namespace SteamAccountManager.App.Tests.Fakes;

/// <summary>Returns whatever the supplied factory yields on each GetAccounts call.</summary>
public sealed class FakeAccountListService : IAccountListService
{
    private readonly Func<IReadOnlyList<AccountListItem>> _factory;
    public FakeAccountListService(Func<IReadOnlyList<AccountListItem>> factory) => _factory = factory;

    public IReadOnlyList<AccountListItem> GetAccounts() => _factory();
}
