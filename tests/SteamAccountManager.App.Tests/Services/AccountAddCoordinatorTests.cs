using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SteamAccountManager.App.Models;
using SteamAccountManager.App.Services;
using SteamAccountManager.App.Tests.Fakes;
using Xunit;

namespace SteamAccountManager.App.Tests.Services;

public class AccountAddCoordinatorTests
{
    private static AccountListItem Item(string id)
        => new(id, "name" + id, "Persona" + id, null, Array.Empty<string>(), false, null);

    [Fact]
    public void FindNewAccounts_ReturnsIdsPresentOnlyInAfter()
    {
        var before = new[] { Item("1"), Item("2") };
        var after = new[] { Item("1"), Item("2"), Item("3") };

        var added = AccountAddCoordinator.FindNewAccounts(before, after);

        Assert.Equal(new[] { "3" }, added.Select(a => a.SteamId64).ToArray());
    }

    [Fact]
    public void FindNewAccounts_ReturnsEmpty_WhenNothingNew()
    {
        var before = new[] { Item("1") };
        var after = new[] { Item("1") };

        Assert.Empty(AccountAddCoordinator.FindNewAccounts(before, after));
    }

    [Fact]
    public async Task BeginAddAndWaitAsync_ReturnsNewAccount_WhenItAppears()
    {
        var calls = 0;
        var listService = new FakeAccountListService(() =>
        {
            calls++;
            // First read (the "before" snapshot) has one account; later reads add a second.
            return calls <= 2
                ? new[] { Item("1") }
                : new[] { Item("1"), Item("2") };
        });
        var switcher = new FakeAccountSwitcher();
        var sut = new AccountAddCoordinator(switcher, listService);

        var result = await sut.BeginAddAndWaitAsync(
            pollInterval: TimeSpan.FromMilliseconds(1),
            timeout: TimeSpan.FromSeconds(2),
            ct: CancellationToken.None);

        Assert.Equal(1, switcher.BeginAddCalls);
        Assert.NotNull(result);
        Assert.Equal("2", result!.SteamId64);
    }

    [Fact]
    public async Task BeginAddAndWaitAsync_ReturnsNull_OnTimeout()
    {
        var listService = new FakeAccountListService(() => new[] { Item("1") });
        var sut = new AccountAddCoordinator(new FakeAccountSwitcher(), listService);

        var result = await sut.BeginAddAndWaitAsync(
            pollInterval: TimeSpan.FromMilliseconds(1),
            timeout: TimeSpan.FromMilliseconds(30),
            ct: CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task BeginAddAndWaitAsync_ReturnsNull_WhenCancelled()
    {
        var listService = new FakeAccountListService(() => new[] { Item("1") });
        var sut = new AccountAddCoordinator(new FakeAccountSwitcher(), listService);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await sut.BeginAddAndWaitAsync(
            pollInterval: TimeSpan.FromMilliseconds(1),
            timeout: TimeSpan.FromSeconds(5),
            ct: cts.Token);

        Assert.Null(result);
    }
}
