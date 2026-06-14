using System;
using System.IO;
using SteamAccountManager.Core.Steam;
using SteamAccountManager.Core.System;
using SteamAccountManager.Core.Tests.Fakes;
using Xunit;

namespace SteamAccountManager.Core.Tests.Steam;

public class SteamLocatorTests
{
    [Fact]
    public void Locate_UsesHkcuSteamPath_AndNormalizesSlashes()
    {
        var reg = new FakeWindowsRegistry();
        reg.SetString(RegistryHiveSelector.CurrentUser, @"Software\Valve\Steam", "SteamPath",
            "C:/Program Files (x86)/Steam");
        var sut = new SteamLocator(reg);

        var paths = sut.Locate();

        Assert.NotNull(paths);
        Assert.Equal(@"C:\Program Files (x86)\Steam", paths!.InstallDirectory);
        Assert.Equal(@"C:\Program Files (x86)\Steam\steam.exe", paths.ExecutablePath);
        Assert.Equal(@"C:\Program Files (x86)\Steam\config\loginusers.vdf", paths.LoginUsersPath);
        // local.vdf lives under LocalAppData regardless of the install dir.
        Assert.Equal(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Steam", "local.vdf"),
            paths.LocalVdfPath);
    }

    [Fact]
    public void Locate_FallsBackToHklmWow6432Node()
    {
        var reg = new FakeWindowsRegistry();
        reg.SetString(RegistryHiveSelector.LocalMachine, @"SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath",
            @"D:\Steam");
        var sut = new SteamLocator(reg);

        var paths = sut.Locate();

        Assert.Equal(@"D:\Steam", paths!.InstallDirectory);
    }

    [Fact]
    public void Locate_FallsBackToHklm32BitInstallPath()
    {
        var reg = new FakeWindowsRegistry();
        reg.SetString(RegistryHiveSelector.LocalMachine, @"SOFTWARE\Valve\Steam", "InstallPath",
            @"E:\Games\Steam");
        var sut = new SteamLocator(reg);

        var paths = sut.Locate();

        Assert.Equal(@"E:\Games\Steam", paths!.InstallDirectory);
    }

    [Fact]
    public void Locate_ReturnsNull_WhenNothingRegistered()
    {
        var sut = new SteamLocator(new FakeWindowsRegistry());
        Assert.Null(sut.Locate());
    }
}
