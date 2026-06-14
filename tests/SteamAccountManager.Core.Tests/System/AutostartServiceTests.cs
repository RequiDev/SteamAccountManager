using SteamAccountManager.Core.System;
using SteamAccountManager.Core.Tests.Fakes;
using Xunit;

namespace SteamAccountManager.Core.Tests.System;

public class AutostartServiceTests
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    [Fact]
    public void Enable_WritesQuotedExePathWithMinimizedFlag()
    {
        var reg = new FakeWindowsRegistry();
        var sut = new AutostartService(reg);

        sut.Enable(@"C:\Apps\SteamAccountManager.exe");

        Assert.True(sut.IsEnabled());
        Assert.Equal(
            "\"C:\\Apps\\SteamAccountManager.exe\" --minimized",
            reg.GetString(RegistryHiveSelector.CurrentUser, RunKey, "SteamAccountManager"));
    }

    [Fact]
    public void Disable_RemovesValue()
    {
        var reg = new FakeWindowsRegistry();
        var sut = new AutostartService(reg);
        sut.Enable(@"C:\Apps\SteamAccountManager.exe");

        sut.Disable();

        Assert.False(sut.IsEnabled());
    }

    [Fact]
    public void IsEnabled_False_WhenNothingWritten()
    {
        Assert.False(new AutostartService(new FakeWindowsRegistry()).IsEnabled());
    }
}
