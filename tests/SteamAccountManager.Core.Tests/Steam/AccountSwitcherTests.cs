using System;
using System.IO;
using System.Linq;
using SteamAccountManager.Core.Models;
using SteamAccountManager.Core.Steam;
using SteamAccountManager.Core.System;
using SteamAccountManager.Core.Tests.Fakes;
using Xunit;

namespace SteamAccountManager.Core.Tests.Steam;

public class AccountSwitcherTests
{
    private const string Vdf = """
        "users"
        {
        	"76561198000000001"
        	{
        		"AccountName"		"alice"
        		"MostRecent"		"1"
        	}
        	"76561198000000002"
        	{
        		"AccountName"		"bob"
        		"MostRecent"		"0"
        	}
        }
        """;

    private sealed record Harness(
        AccountSwitcher Switcher,
        FakeWindowsRegistry Registry,
        SteamRegistry SteamRegistry,
        FakeProcessController Process,
        string LoginUsersPath,
        string LocalVdfPath,
        ConnectCacheStore ConnectCache);

    private static Harness BuildHarness(TestPaths tmp, ILoginUsersStore? loginUsersOverride = null)
    {
        var loginUsersPath = tmp.WriteFile("config/loginusers.vdf", Vdf);
        var localVdfPath = tmp.File("localappdata/Steam/local.vdf");
        Directory.CreateDirectory(Path.GetDirectoryName(localVdfPath)!);
        var paths = new SteamPaths(
            tmp.Root, Path.Combine(tmp.Root, "steam.exe"),
            tmp.File("config"), loginUsersPath, localVdfPath);

        var reg = new FakeWindowsRegistry();
        var steamReg = new SteamRegistry(reg);
        var process = new FakeProcessController();
        var atomic = new AtomicFile();
        var loginUsers = loginUsersOverride ?? new LoginUsersStore(atomic);
        var backup = new BackupService(tmp.File("backups"));
        var connectCache = new ConnectCacheStore(tmp.File("connectcache.json"), atomic);

        var switcher = new AccountSwitcher(
            new FakeSteamLocator(paths), loginUsers, steamReg, process, backup, connectCache);

        return new Harness(switcher, reg, steamReg, process, loginUsersPath, localVdfPath, connectCache);
    }

    [Fact]
    public void SwitchTo_SetsRegistryAndFile_AndLaunches_WhenSteamNotRunning()
    {
        using var tmp = new TestPaths();
        var h = BuildHarness(tmp);

        h.Switcher.SwitchTo("76561198000000002");

        Assert.Equal("bob", h.SteamRegistry.GetAutoLoginUser());
        Assert.True(h.SteamRegistry.GetRememberPassword());
        // Semantic re-read (the literal-tab assertion proved brittle on this machine):
        // confirm the active account flipped to bob in the written VDF.
        var bob = new LoginUsersStore(new AtomicFile())
            .Read(h.LoginUsersPath)
            .Single(a => a.SteamId64 == "76561198000000002");
        Assert.True(bob.MostRecent);
        Assert.True(h.Process.LaunchCalled);
        Assert.False(h.Process.ShutdownCalled);
    }

    [Fact]
    public void SwitchTo_ShutsDownFirst_WhenSteamRunning()
    {
        using var tmp = new TestPaths();
        var h = BuildHarness(tmp);
        h.Process.Running = true;

        h.Switcher.SwitchTo("76561198000000002");

        Assert.True(h.Process.ShutdownCalled);
        Assert.True(h.Process.LaunchCalled);
        // Steam MUST be shut down before it is relaunched (safety-critical ordering).
        Assert.Equal(new[] { "shutdown", "launch" }, h.Process.Calls);
    }

    [Fact]
    public void SwitchTo_Throws_AndDoesNotLaunch_WhenShutdownFails()
    {
        using var tmp = new TestPaths();
        var h = BuildHarness(tmp);
        h.Process.Running = true;
        h.Process.ShutdownResult = false;

        Assert.Throws<SteamShutdownException>(() => h.Switcher.SwitchTo("76561198000000002"));
        Assert.False(h.Process.LaunchCalled);
    }

    [Fact]
    public void SwitchTo_Throws_WhenAccountUnknown()
    {
        using var tmp = new TestPaths();
        var h = BuildHarness(tmp);

        Assert.Throws<AccountNotFoundException>(() => h.Switcher.SwitchTo("76561190000000000"));
    }

    [Fact]
    public void SwitchTo_RestoresFileAndRegistry_WhenWriteFails()
    {
        using var tmp = new TestPaths();
        var inner = new LoginUsersStore(new AtomicFile());
        var h = BuildHarness(tmp, new ThrowingLoginUsersStore(inner));
        h.SteamRegistry.SetAutoLoginUser("previous-user");
        var original = File.ReadAllText(h.LoginUsersPath);

        Assert.Throws<InvalidOperationException>(() => h.Switcher.SwitchTo("76561198000000002"));

        Assert.Equal(original, File.ReadAllText(h.LoginUsersPath));   // file restored
        Assert.Equal("previous-user", h.SteamRegistry.GetAutoLoginUser()); // registry restored
        Assert.False(h.Process.LaunchCalled);
    }

    [Fact]
    public void SwitchTo_ClearsAutoLogin_OnFailure_WhenNoPriorValue()
    {
        using var tmp = new TestPaths();
        var inner = new LoginUsersStore(new AtomicFile());
        var h = BuildHarness(tmp, new ThrowingLoginUsersStore(inner));
        // No prior AutoLoginUser is set.

        Assert.Throws<InvalidOperationException>(() => h.Switcher.SwitchTo("76561198000000002"));

        // Rollback must not strand the target account name in the registry.
        Assert.Equal("", h.SteamRegistry.GetAutoLoginUser());
        Assert.False(h.Process.LaunchCalled);
    }

    [Fact]
    public void SwitchTo_PreservesConnectCacheTokens_AcrossSwitches()
    {
        using var tmp = new TestPaths();
        var h = BuildHarness(tmp);
        // local.vdf currently holds a token for an account that the next state will have pruned.
        File.WriteAllText(h.LocalVdfPath, ConnectCacheVdf(("15189d6c1", "01000000AAAA")));

        // First switch captures the token into the store...
        h.Switcher.SwitchTo("76561198000000002");

        // Steam then prunes it (local.vdf no longer has that account)...
        File.WriteAllText(h.LocalVdfPath, ConnectCacheVdf(("cc631c8f1", "01000000BBBB")));

        // ...and the next switch re-injects the preserved token, so both accounts stay cached.
        h.Switcher.SwitchTo("76561198000000001");

        var keys = ConnectCacheKeys(h.LocalVdfPath);
        Assert.Contains("15189d6c1", keys); // preserved
        Assert.Contains("cc631c8f1", keys);
        Assert.True(h.Process.LaunchCalled);
    }

    private static string ConnectCacheVdf(params (string key, string val)[] entries)
    {
        var sb = new global::System.Text.StringBuilder();
        sb.Append("\"MachineUserConfigStore\"\n{\n\t\"Software\"\n\t{\n\t\t\"Valve\"\n\t\t{\n\t\t\t\"Steam\"\n\t\t\t{\n\t\t\t\t\"ConnectCache\"\n\t\t\t\t{\n");
        foreach (var (k, v) in entries)
        {
            sb.Append($"\t\t\t\t\t\"{k}\"\t\t\"{v}\"\n");
        }

        sb.Append("\t\t\t\t}\n\t\t\t}\n\t\t}\n\t}\n}\n");
        return sb.ToString();
    }

    private static global::System.Collections.Generic.List<string> ConnectCacheKeys(string vdfPath)
    {
        var ser = global::ValveKeyValue.KVSerializer.Create(global::ValveKeyValue.KVSerializationFormat.KeyValues1Text);
        using var fs = File.OpenRead(vdfPath);
        var cc = ser.Deserialize(fs).Root["Software"]["Valve"]["Steam"]["ConnectCache"];
        var keys = new global::System.Collections.Generic.List<string>();
        foreach (var kv in cc)
        {
            keys.Add(kv.Key);
        }

        return keys;
    }

    [Fact]
    public void BeginAddAccount_ClearsAutoLogin_AndLaunches()
    {
        using var tmp = new TestPaths();
        var h = BuildHarness(tmp);
        h.SteamRegistry.SetAutoLoginUser("alice");

        h.Switcher.BeginAddAccount();

        Assert.Equal("", h.SteamRegistry.GetAutoLoginUser());
        Assert.True(h.Process.LaunchCalled);
    }
}
