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
        string TokenStoreDir);

    private static Harness BuildHarness(TestPaths tmp, ILoginUsersStore? loginUsersOverride = null)
    {
        var loginUsersPath = tmp.WriteFile("config/loginusers.vdf", Vdf);
        var localVdfPath = tmp.File("localappdata/Steam/local.vdf");
        Directory.CreateDirectory(Path.GetDirectoryName(localVdfPath)!);
        var tokenStoreDir = tmp.File("tokens");
        var paths = new SteamPaths(
            tmp.Root, Path.Combine(tmp.Root, "steam.exe"),
            tmp.File("config"), loginUsersPath, localVdfPath);

        var reg = new FakeWindowsRegistry();
        var steamReg = new SteamRegistry(reg);
        var process = new FakeProcessController();
        var atomic = new AtomicFile();
        var loginUsers = loginUsersOverride ?? new LoginUsersStore(atomic);
        var backup = new BackupService(tmp.File("backups"));
        var tokenStore = new SteamTokenStore(tokenStoreDir);

        var switcher = new AccountSwitcher(
            new FakeSteamLocator(paths), loginUsers, steamReg, process, backup, tokenStore);

        return new Harness(switcher, reg, steamReg, process, loginUsersPath, localVdfPath, tokenStoreDir);
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
    public void SwitchTo_CapturesOutgoingToken_AndRestoresTargetToken()
    {
        using var tmp = new TestPaths();
        var h = BuildHarness(tmp);
        // alice (76561198000000001) is currently logged in -> SteamID3 39734273.
        h.Registry.SetDword(RegistryHiveSelector.CurrentUser, @"Software\Valve\Steam\ActiveProcess", "ActiveUser", 39734273);
        File.WriteAllText(h.LocalVdfPath, "ALICE_TOKEN"); // local.vdf currently holds alice's token
        Directory.CreateDirectory(h.TokenStoreDir);
        File.WriteAllText(Path.Combine(h.TokenStoreDir, "76561198000000002.local.vdf"), "BOB_TOKEN"); // bob already saved

        h.Switcher.SwitchTo("76561198000000002");

        // Outgoing (alice) token captured before the switch...
        Assert.Equal("ALICE_TOKEN", File.ReadAllText(Path.Combine(h.TokenStoreDir, "76561198000000001.local.vdf")));
        // ...and the target (bob) token restored into local.vdf so Steam can silently auto-login.
        Assert.Equal("BOB_TOKEN", File.ReadAllText(h.LocalVdfPath));
    }

    [Fact]
    public void SwitchTo_LeavesLocalVdfUnchanged_WhenTargetHasNoSavedToken()
    {
        using var tmp = new TestPaths();
        var h = BuildHarness(tmp);
        File.WriteAllText(h.LocalVdfPath, "EXISTING"); // not logged in; no saved token for bob

        h.Switcher.SwitchTo("76561198000000002");

        Assert.Equal("EXISTING", File.ReadAllText(h.LocalVdfPath)); // nothing to restore -> untouched
        Assert.True(h.Process.LaunchCalled);
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
