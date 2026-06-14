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
        return new TrayViewModel(listService, groups, switcher, autostart, settings, shell);
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
            autostart, settingsStore, new FakeShellController());

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
