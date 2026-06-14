using System.IO;
using System.Linq;
using SteamAccountManager.Core.Models;
using SteamAccountManager.Core.Steam;
using SteamAccountManager.Core.System;
using Xunit;

namespace SteamAccountManager.Core.Tests.Steam;

public class LoginUsersStoreWriteTests
{
    private const string Vdf = """
        "users"
        {
        	"76561198000000001"
        	{
        		"AccountName"		"alice"
        		"PersonaName"		"Alice"
        		"RememberPassword"		"1"
        		"WantsOfflineMode"		"0"
        		"AllowAutoLogin"		"1"
        		"MostRecent"		"1"
        		"Timestamp"		"1688740727"
        	}
        	"76561198000000002"
        	{
        		"AccountName"		"bob"
        		"PersonaName"		"Bob"
        		"RememberPassword"		"0"
        		"AllowAutoLogin"		"0"
        		"MostRecent"		"0"
        		"Timestamp"		"1688740000"
        	}
        }
        """;

    [Fact]
    public void SetActiveAccount_FlipsMostRecentAndRemember_AndPreservesOtherFields()
    {
        using var tmp = new TestPaths();
        var path = tmp.WriteFile("config/loginusers.vdf", Vdf);
        var sut = new LoginUsersStore(new AtomicFile());

        sut.SetActiveAccount(path, "76561198000000002");

        var accounts = sut.Read(path);
        var alice = accounts.Single(a => a.SteamId64 == "76561198000000001");
        var bob = accounts.Single(a => a.SteamId64 == "76561198000000002");

        Assert.False(alice.MostRecent);
        Assert.True(bob.MostRecent);
        Assert.True(bob.RememberPassword);

        // Unknown/unmodelled fields must survive the round-trip.
        var raw = File.ReadAllText(path);
        Assert.Contains("WantsOfflineMode", raw);
        Assert.Contains("Bob", raw);
        Assert.Contains("1688740000", raw);
    }

    [Fact]
    public void SetActiveAccount_Throws_WhenAccountMissing()
    {
        using var tmp = new TestPaths();
        var path = tmp.WriteFile("config/loginusers.vdf", Vdf);
        var sut = new LoginUsersStore(new AtomicFile());

        Assert.Throws<AccountNotFoundException>(
            () => sut.SetActiveAccount(path, "76561190000000000"));
    }
}
