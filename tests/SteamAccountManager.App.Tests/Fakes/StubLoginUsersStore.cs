using System.Collections.Generic;
using SteamAccountManager.Core.Models;
using SteamAccountManager.Core.Steam;

namespace SteamAccountManager.App.Tests.Fakes;

/// <summary>In-memory ILoginUsersStore. Records SwitchTo calls; returns a settable account list.</summary>
public sealed class StubLoginUsersStore : ILoginUsersStore
{
    public List<SteamAccount> Accounts { get; set; } = new();
    public List<(string Path, string SteamId64)> SetActiveCalls { get; } = new();

    public IReadOnlyList<SteamAccount> Read(string loginUsersPath) => Accounts;

    public void SetActiveAccount(string loginUsersPath, string steamId64)
        => SetActiveCalls.Add((loginUsersPath, steamId64));
}
