using System.Linq;
using SteamAccountManager.Core.Steam;
using SteamAccountManager.Core.System;
using Xunit;

namespace SteamAccountManager.Core.Tests.Steam;

public class LoginUsersStoreReadTests
{
    private const string ModernVdf = """
        "users"
        {
        	"76561198000000001"
        	{
        		"AccountName"		"alice"
        		"PersonaName"		"Alice"
        		"RememberPassword"		"1"
        		"WantsOfflineMode"		"0"
        		"SkipOfflineModeWarning"		"0"
        		"AllowAutoLogin"		"1"
        		"MostRecent"		"1"
        		"Timestamp"		"1688740727"
        	}
        	"76561198000000002"
        	{
        		"AccountName"		"bob"
        		"PersonaName"		"Bob"
        		"RememberPassword"		"1"
        		"AllowAutoLogin"		"0"
        		"MostRecent"		"0"
        		"Timestamp"		"1688740000"
        	}
        }
        """;

    // Legacy/lowercase-cased field names must still be read.
    private const string LowercaseVdf = """
        "users"
        {
        	"76561198000000003"
        	{
        		"accountname"		"carol"
        		"personaname"		"Carol"
        		"mostrecent"		"1"
        	}
        }
        """;

    [Fact]
    public void Read_ParsesAllAccountsAndFields()
    {
        using var tmp = new TestPaths();
        var path = tmp.WriteFile("config/loginusers.vdf", ModernVdf);
        var sut = new LoginUsersStore(new AtomicFile());

        var accounts = sut.Read(path);

        Assert.Equal(2, accounts.Count);
        var alice = accounts.Single(a => a.SteamId64 == "76561198000000001");
        Assert.Equal("alice", alice.AccountName);
        Assert.Equal("Alice", alice.PersonaName);
        Assert.True(alice.MostRecent);
        Assert.True(alice.RememberPassword);
        Assert.True(alice.AllowAutoLogin);
        Assert.Equal(1688740727L, alice.Timestamp);

        var bob = accounts.Single(a => a.SteamId64 == "76561198000000002");
        Assert.False(bob.MostRecent);
    }

    [Fact]
    public void Read_IsCaseInsensitiveOnFieldNames()
    {
        using var tmp = new TestPaths();
        var path = tmp.WriteFile("config/loginusers.vdf", LowercaseVdf);
        var sut = new LoginUsersStore(new AtomicFile());

        var carol = sut.Read(path).Single();

        Assert.Equal("carol", carol.AccountName);
        Assert.True(carol.MostRecent);
    }

    [Fact]
    public void Read_ReturnsEmpty_WhenFileMissing()
    {
        using var tmp = new TestPaths();
        var sut = new LoginUsersStore(new AtomicFile());

        var accounts = sut.Read(tmp.File("nope.vdf"));

        Assert.Empty(accounts);
    }
}
