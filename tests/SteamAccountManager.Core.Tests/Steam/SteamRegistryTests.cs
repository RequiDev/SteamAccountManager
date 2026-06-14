using SteamAccountManager.Core.Steam;
using SteamAccountManager.Core.System;
using SteamAccountManager.Core.Tests.Fakes;
using Xunit;

namespace SteamAccountManager.Core.Tests.Steam;

public class SteamRegistryTests
{
    private const string SteamKey = @"Software\Valve\Steam";

    [Fact]
    public void SetAutoLoginUser_And_GetAutoLoginUser_RoundTrip()
    {
        var reg = new FakeWindowsRegistry();
        var sut = new SteamRegistry(reg);

        sut.SetAutoLoginUser("alice");

        Assert.Equal("alice", sut.GetAutoLoginUser());
        Assert.Equal("alice", reg.GetString(RegistryHiveSelector.CurrentUser, SteamKey, "AutoLoginUser"));
    }

    [Fact]
    public void ClearAutoLoginUser_SetsEmptyString()
    {
        var reg = new FakeWindowsRegistry();
        var sut = new SteamRegistry(reg);
        sut.SetAutoLoginUser("alice");

        sut.ClearAutoLoginUser();

        Assert.Equal("", sut.GetAutoLoginUser());
    }

    [Fact]
    public void SetRememberPassword_WritesDword()
    {
        var reg = new FakeWindowsRegistry();
        var sut = new SteamRegistry(reg);

        sut.SetRememberPassword(true);
        Assert.True(sut.GetRememberPassword());
        Assert.Equal(1, reg.GetDword(RegistryHiveSelector.CurrentUser, SteamKey, "RememberPassword"));

        sut.SetRememberPassword(false);
        Assert.False(sut.GetRememberPassword());
    }
}
