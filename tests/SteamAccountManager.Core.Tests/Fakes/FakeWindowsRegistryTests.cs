using SteamAccountManager.Core.System;
using Xunit;

namespace SteamAccountManager.Core.Tests.Fakes;

public class FakeWindowsRegistryTests
{
    [Fact]
    public void RoundTripsStringsAndDwords_AndDeletes()
    {
        var reg = new FakeWindowsRegistry();

        Assert.Null(reg.GetString(RegistryHiveSelector.CurrentUser, @"Software\X", "S"));

        reg.SetString(RegistryHiveSelector.CurrentUser, @"Software\X", "S", "hello");
        reg.SetDword(RegistryHiveSelector.CurrentUser, @"Software\X", "D", 1);

        Assert.Equal("hello", reg.GetString(RegistryHiveSelector.CurrentUser, @"Software\X", "S"));
        Assert.Equal(1, reg.GetDword(RegistryHiveSelector.CurrentUser, @"Software\X", "D"));

        reg.DeleteValue(RegistryHiveSelector.CurrentUser, @"Software\X", "S");
        Assert.Null(reg.GetString(RegistryHiveSelector.CurrentUser, @"Software\X", "S"));
    }
}
