using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SteamAccountManager.App.Models;
using SteamAccountManager.App.Tests.Fakes;
using SteamAccountManager.App.ViewModels;
using SteamAccountManager.Core.Models;
using SteamAccountManager.Core.Storage;
using SteamAccountManager.Core.System;
using Xunit;

namespace SteamAccountManager.App.Tests.ViewModels;

public class TrayViewModelTests
{
    private static AccountListItem Item(string id, bool active = false, params string[] groups)
        => new(id, "name" + id, "Display" + id, null, groups, active, null);

    private static TrayViewModel Build(
        IReadOnlyList<AccountListItem> accounts,
        FakeGroupManagementService groups,
        out FakeShellController shell,
        out FakeAutostartService autostart,
        out FakeAccountSwitcher switcher,
        TestPaths tmp)
    {
        shell = new FakeShellController();
        autostart = new FakeAutostartService();
        switcher = new FakeAccountSwitcher();
        var listService = new FakeAccountListService(() => accounts);
        var settings = new SettingsStore(tmp.File("settings.json"), new AtomicFile());
        return new TrayViewModel(listService, groups, switcher, autostart, settings, shell, new FakeUpdateCoordinator());
    }

    [Fact]
    public void Rebuild_GroupsAccounts_AndAddsUngroupedBucket()
    {
        using var tmp = new TestPaths();
        var groups = new FakeGroupManagementService();
        groups.AddExisting("g1", "Work");
        var sut = Build(
            new[] { Item("1", true, "g1"), Item("2") },
            groups, out _, out _, out _, tmp);

        sut.Rebuild();

        var work = sut.Groups.Single(g => g.Header == "Work");
        Assert.Single(work.Accounts);
        Assert.True(work.Accounts[0].IsActive);

        var ungrouped = sut.Groups.Single(g => g.Header == "Ungrouped");
        Assert.Equal("2", ungrouped.Accounts.Single().SteamId64);
    }

    [Fact]
    public void Rebuild_OmitsUngrouped_WhenAllAccountsGrouped()
    {
        using var tmp = new TestPaths();
        var groups = new FakeGroupManagementService();
        groups.AddExisting("g1", "Work");
        var sut = Build(new[] { Item("1", false, "g1") }, groups, out _, out _, out _, tmp);

        sut.Rebuild();

        Assert.DoesNotContain(sut.Groups, g => g.Header == "Ungrouped");
    }

    [Fact]
    public void Rebuild_TrayItems_AppendLoginName_SoDuplicateDisplayNamesAreDistinct()
    {
        using var tmp = new TestPaths();
        // Two accounts sharing a display name (persona) but with distinct Steam login names.
        var a = new AccountListItem("76561190000000001", "login_a", "SameName", null, Array.Empty<string>(), false, null);
        var b = new AccountListItem("76561190000000002", "login_b", "SameName", null, Array.Empty<string>(), false, null);
        var sut = Build(new[] { a, b }, new FakeGroupManagementService(), out _, out _, out _, tmp);

        sut.Rebuild();

        var labels = sut.Groups.Single(g => g.Header == "Ungrouped").Accounts.Select(x => x.MenuLabel).ToList();
        Assert.Contains("SameName (login_a)", labels);
        Assert.Contains("SameName (login_b)", labels);
    }

    [Theory]
    [InlineData("Yoshino", "requi_cs2", "Yoshino (requi_cs2)")]
    [InlineData("alice", "alice", "alice")] // no redundant "alice (alice)" when display == login
    public void TrayAccountItem_MenuLabel_AppendsLoginName_ExceptWhenIdentical(
        string display, string account, string expected)
    {
        var item = new TrayAccountItem("76561190000000000", display, account, false);
        Assert.Equal(expected, item.MenuLabel);
    }

    [Fact]
    public void ShowWindowCommand_DelegatesToShell()
    {
        using var tmp = new TestPaths();
        var sut = Build(Array.Empty<AccountListItem>(), new FakeGroupManagementService(),
            out var shell, out _, out _, tmp);

        sut.ShowWindowCommand.Execute(null);

        Assert.Equal(1, shell.ShowCalls);
    }

    [Fact]
    public void ExitCommand_DelegatesToShell()
    {
        using var tmp = new TestPaths();
        var sut = Build(Array.Empty<AccountListItem>(), new FakeGroupManagementService(),
            out var shell, out _, out _, tmp);

        sut.ExitCommand.Execute(null);

        Assert.Equal(1, shell.ExitCalls);
    }

    [Fact]
    public void ToggleAutostartCommand_EnablesAndPersists()
    {
        using var tmp = new TestPaths();
        var settingsStore = new SettingsStore(tmp.File("settings.json"), new AtomicFile());
        var listService = new FakeAccountListService(() => Array.Empty<AccountListItem>());
        var autostart = new FakeAutostartService();
        var sut = new TrayViewModel(
            listService, new FakeGroupManagementService(), new FakeAccountSwitcher(),
            autostart, settingsStore, new FakeShellController(), new FakeUpdateCoordinator());

        Assert.False(sut.IsAutostartEnabled);

        sut.ToggleAutostartCommand.Execute(null);

        Assert.True(sut.IsAutostartEnabled);
        Assert.True(autostart.IsEnabled());
        Assert.True(settingsStore.Load().AutostartEnabled);

        sut.ToggleAutostartCommand.Execute(null);
        Assert.False(sut.IsAutostartEnabled);
        Assert.False(autostart.IsEnabled());
        Assert.False(settingsStore.Load().AutostartEnabled);
    }

    [Fact]
    public async Task CheckForUpdatesCommand_DelegatesToCoordinator_AsUserInitiated()
    {
        using var tmp = new TestPaths();
        var updates = new FakeUpdateCoordinator();
        var settings = new SettingsStore(tmp.File("settings.json"), new AtomicFile());
        var sut = new TrayViewModel(
            new FakeAccountListService(() => Array.Empty<AccountListItem>()),
            new FakeGroupManagementService(), new FakeAccountSwitcher(),
            new FakeAutostartService(), settings, new FakeShellController(), updates);

        await sut.CheckForUpdatesCommand.ExecuteAsync(null);

        Assert.Equal(new[] { true }, updates.Checks.ToArray());
    }

    [Fact]
    public async Task SwitchAccountCommand_DelegatesToSwitcher()
    {
        using var tmp = new TestPaths();
        var sut = Build(new[] { Item("1") }, new FakeGroupManagementService(),
            out var shell, out _, out var switcher, tmp);

        // ExecuteAsync (on the generated IAsyncRelayCommand) awaits the Task.Run-backed switch,
        // so the assertion runs after completion — no race.
        await sut.SwitchAccountCommand.ExecuteAsync("1");

        Assert.Equal(new[] { "1" }, switcher.SwitchedTo.ToArray());
        Assert.Equal(1, shell.RefreshCalls); // tray switch refreshes the dashboard
    }
}
