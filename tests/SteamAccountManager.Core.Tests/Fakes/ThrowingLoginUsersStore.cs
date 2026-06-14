using System;
using System.Collections.Generic;
using SteamAccountManager.Core.Models;
using SteamAccountManager.Core.Steam;

namespace SteamAccountManager.Core.Tests.Fakes;

/// <summary>Reads via a real delegate but throws on write, to exercise rollback.</summary>
public sealed class ThrowingLoginUsersStore : ILoginUsersStore
{
    private readonly ILoginUsersStore _inner;
    public ThrowingLoginUsersStore(ILoginUsersStore inner) => _inner = inner;

    public IReadOnlyList<SteamAccount> Read(string loginUsersPath) => _inner.Read(loginUsersPath);

    public void SetActiveAccount(string loginUsersPath, string steamId64)
        => throw new InvalidOperationException("simulated write failure");
}
