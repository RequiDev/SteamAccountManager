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

    [Fact]
    public void GetActiveAccountSteamId64_ConvertsActiveUserAccountIdToSteamId64()
    {
        var reg = new FakeWindowsRegistry();
        // ActiveUser holds the SteamID3 (account id); 39734273 -> SteamID64 76561198000000001.
        reg.SetDword(RegistryHiveSelector.CurrentUser, @"Software\Valve\Steam\ActiveProcess", "ActiveUser", 39734273);
        var sut = new SteamRegistry(reg);

        Assert.Equal("76561198000000001", sut.GetActiveAccountSteamId64());
    }

    [Fact]
    public void GetActiveAccountSteamId64_ReturnsNull_WhenNotLoggedIn()
    {
        var reg = new FakeWindowsRegistry();
        var sut = new SteamRegistry(reg);
        Assert.Null(sut.GetActiveAccountSteamId64()); // no ActiveUser value

        reg.SetDword(RegistryHiveSelector.CurrentUser, @"Software\Valve\Steam\ActiveProcess", "ActiveUser", 0);
        Assert.Null(sut.GetActiveAccountSteamId64()); // 0 = at login screen
    }
}
