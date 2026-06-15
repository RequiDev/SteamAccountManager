using System;
using SteamAccountManager.App.Infrastructure;
using Xunit;

namespace SteamAccountManager.App.Tests.Infrastructure;

public class UpdateApplierTests
{
    [Fact]
    public void TryParse_ExtractsTargetAndPid()
    {
        var ok = UpdateApplier.TryParse(
            new[] { "--apply-update", @"C:\apps\SteamAccountManager.App.exe", "12345" },
            out var target, out var pid);

        Assert.True(ok);
        Assert.Equal(@"C:\apps\SteamAccountManager.App.exe", target);
        Assert.Equal(12345, pid);
    }

    [Fact]
    public void TryParse_SkipsLeadingArgs_AndIsCaseInsensitive()
    {
        var ok = UpdateApplier.TryParse(
            new[] { "--minimized", "--APPLY-UPDATE", @"C:\a b\app.exe", "777" },
            out var target, out var pid);

        Assert.True(ok);
        Assert.Equal(@"C:\a b\app.exe", target);
        Assert.Equal(777, pid);
    }

    [Fact]
    public void TryParse_Fails_OnMissingOrBadArgs()
    {
        Assert.False(UpdateApplier.TryParse(Array.Empty<string>(), out _, out _));
        Assert.False(UpdateApplier.TryParse(new[] { "--minimized" }, out _, out _));
        Assert.False(UpdateApplier.TryParse(new[] { "--apply-update" }, out _, out _));
        Assert.False(UpdateApplier.TryParse(new[] { "--apply-update", @"C:\app.exe" }, out _, out _));
        Assert.False(UpdateApplier.TryParse(new[] { "--apply-update", @"C:\app.exe", "notapid" }, out _, out _));
    }
}
