using System.Linq;
using SteamAccountManager.App.Services;
using SteamAccountManager.App.Tests.Fakes;
using SteamAccountManager.Core.Models;
using SteamAccountManager.Core.Storage;
using SteamAccountManager.Core.System;
using Xunit;

namespace SteamAccountManager.App.Tests.Services;

public class AccountListServiceTests
{
    private static SteamPaths Paths(string loginUsersPath)
        => new("C:\\Steam", "C:\\Steam\\steam.exe", "C:\\Steam\\config", loginUsersPath, "C:\\Steam\\local.vdf");

    private static SteamAccount Account(string id, string name, string persona, bool active = false)
        => new() { SteamId64 = id, AccountName = name, PersonaName = persona, MostRecent = active };

    [Fact]
    public void GetAccounts_ReturnsEmpty_WhenSteamNotFound()
    {
        using var tmp = new TestPaths();
        var atomic = new AtomicFile();
        var meta = new AccountMetadataStore(tmp.File("metadata.json"), atomic);

        var sut = new AccountListService(
            new FakeSteamLocator(null),
            new StubLoginUsersStore(),
            meta,
            new FakeConnectCacheStore());

        Assert.Empty(sut.GetAccounts());
    }

    [Fact]
    public void GetAccounts_JoinsAccountsWithMetadata()
    {
        using var tmp = new TestPaths();
        var atomic = new AtomicFile();
        var meta = new AccountMetadataStore(tmp.File("metadata.json"), atomic);
        meta.Upsert(new AccountMetadata
        {
            SteamId64 = "1",
            CustomLabel = "Main",
            GroupIds = { "g1" },
        });

        var loginUsers = new StubLoginUsersStore
        {
            Accounts =
            {
                Account("1", "alice", "Alice", active: true),
                Account("2", "bob", "Bob"),
            },
        };

        var connectCache = new FakeConnectCacheStore();
        connectCache.CachedAccounts.Add("alice"); // alice has a cached token, bob does not

        var sut = new AccountListService(
            new FakeSteamLocator(Paths(tmp.File("loginusers.vdf"))),
            loginUsers,
            meta,
            connectCache);

        var items = sut.GetAccounts();

        Assert.Equal(2, items.Count);
        var alice = items.Single(i => i.SteamId64 == "1");
        Assert.Equal("Main", alice.DisplayName);
        Assert.True(alice.IsActive);
        Assert.Equal(new[] { "g1" }, alice.GroupIds);
        Assert.True(alice.IsTokenCached);

        var bob = items.Single(i => i.SteamId64 == "2");
        Assert.Equal("Bob", bob.DisplayName);
        Assert.Empty(bob.GroupIds);
        Assert.False(bob.IsTokenCached);
    }
}
