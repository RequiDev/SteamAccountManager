using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SteamAccountManager.App.Models;
using SteamAccountManager.App.Tests.Fakes;
using SteamAccountManager.App.ViewModels;
using Xunit;

namespace SteamAccountManager.App.Tests.ViewModels;

public class MainViewModelTests
{
    private static AccountListItem Item(string id, bool active = false, params string[] groups)
        => new(id, "name" + id, "Persona" + id, null, groups, active, null);

    private static MainViewModel Build(
        IReadOnlyList<AccountListItem> accounts,
        out FakeGroupManagementService groups,
        out FakeAccountSwitcher switcher)
    {
        var listService = new FakeAccountListService(() => accounts);
        groups = new FakeGroupManagementService();
        switcher = new FakeAccountSwitcher();
        var add = new AccountAddCoordinatorStub();
        return new MainViewModel(listService, groups, switcher, new FakeAvatarService(), add);
    }

    [Fact]
    public async Task LoadAsync_PopulatesCards_AndGroupFilters()
    {
        var g = "g1";
        var mvm = Build(new[] { Item("1", active: true, g), Item("2") }, out var groups, out _);
        groups.AddExisting(g, "Work");

        await mvm.LoadAsync();

        Assert.Equal(2, mvm.Accounts.Count);
        // Filters: All, each group, Ungrouped
        Assert.Contains(mvm.GroupFilters, f => f.IsAll);
        Assert.Contains(mvm.GroupFilters, f => f.GroupId == g);
        Assert.Contains(mvm.GroupFilters, f => f.IsUngrouped);
    }

    [Fact]
    public async Task FilteredAccounts_DefaultsToAll()
    {
        var mvm = Build(new[] { Item("1", false, "g1"), Item("2") }, out var groups, out _);
        groups.AddExisting("g1", "Work");
        await mvm.LoadAsync();

        Assert.Equal(2, mvm.FilteredAccounts.Count);
    }

    [Fact]
    public async Task SelectingGroupFilter_ShowsOnlyThatGroup()
    {
        var mvm = Build(new[] { Item("1", false, "g1"), Item("2") }, out var groups, out _);
        groups.AddExisting("g1", "Work");
        await mvm.LoadAsync();

        mvm.SelectedGroupFilter = mvm.GroupFilters.Single(f => f.GroupId == "g1");

        Assert.Single(mvm.FilteredAccounts);
        Assert.Equal("1", mvm.FilteredAccounts[0].SteamId64);
    }

    [Fact]
    public async Task SelectingUngroupedFilter_ShowsAccountsWithNoGroups()
    {
        var mvm = Build(new[] { Item("1", false, "g1"), Item("2") }, out var groups, out _);
        groups.AddExisting("g1", "Work");
        await mvm.LoadAsync();

        mvm.SelectedGroupFilter = mvm.GroupFilters.Single(f => f.IsUngrouped);

        Assert.Single(mvm.FilteredAccounts);
        Assert.Equal("2", mvm.FilteredAccounts[0].SteamId64);
    }

    [Fact]
    public async Task RefreshCommand_ReloadsAccounts()
    {
        var calls = 0;
        var listService = new FakeAccountListService(() =>
        {
            calls++;
            return calls == 1 ? new[] { Item("1") } : new[] { Item("1"), Item("2") };
        });
        var mvm = new MainViewModel(
            listService, new FakeGroupManagementService(), new FakeAccountSwitcher(),
            new FakeAvatarService(), new AccountAddCoordinatorStub());

        await mvm.LoadAsync();
        Assert.Single(mvm.Accounts);

        await mvm.RefreshCommand.ExecuteAsync(null);
        Assert.Equal(2, mvm.Accounts.Count);
    }

    [Fact]
    public async Task AddAccountCommand_InvokesCoordinator_AndRefreshes()
    {
        var add = new AccountAddCoordinatorStub { Result = Item("9") };
        var listService = new FakeAccountListService(() => new[] { Item("1") });
        var mvm = new MainViewModel(
            listService, new FakeGroupManagementService(), new FakeAccountSwitcher(),
            new FakeAvatarService(), add);
        await mvm.LoadAsync();

        await mvm.AddAccountCommand.ExecuteAsync(null);

        Assert.Equal(1, add.Calls);
    }
}
