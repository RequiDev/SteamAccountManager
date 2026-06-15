using SteamAccountManager.Core.Models;
using SteamAccountManager.Core.Storage;
using SteamAccountManager.Core.System;
using Xunit;

namespace SteamAccountManager.Core.Tests.Storage;

public class SettingsStoreTests
{
    [Fact]
    public void Load_ReturnsDefaults_WhenFileMissing()
    {
        using var tmp = new TestPaths();
        var sut = new SettingsStore(tmp.File("settings.json"), new AtomicFile());

        var settings = sut.Load();

        Assert.False(settings.AutostartEnabled);
        Assert.False(settings.StartMinimized);
        Assert.True(settings.CheckForUpdatesOnStartup);
        Assert.Equal(980, settings.WindowWidth);
        Assert.Equal(640, settings.WindowHeight);
        Assert.False(settings.WindowMaximized);
    }

    [Fact]
    public void Save_ThenLoad_RoundTrips()
    {
        using var tmp = new TestPaths();
        var path = tmp.File("settings.json");
        var atomic = new AtomicFile();

        new SettingsStore(path, atomic).Save(new AppSettings
        {
            AutostartEnabled = true,
            StartMinimized = true,
            WindowWidth = 1200,
            WindowHeight = 800,
            WindowMaximized = true,
        });

        var settings = new SettingsStore(path, atomic).Load();

        Assert.True(settings.AutostartEnabled);
        Assert.True(settings.StartMinimized);
        Assert.Equal(1200, settings.WindowWidth);
        Assert.Equal(800, settings.WindowHeight);
        Assert.True(settings.WindowMaximized);
    }
}
