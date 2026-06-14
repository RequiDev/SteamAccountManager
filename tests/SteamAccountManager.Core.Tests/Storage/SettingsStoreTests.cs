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
        });

        var settings = new SettingsStore(path, atomic).Load();

        Assert.True(settings.AutostartEnabled);
        Assert.True(settings.StartMinimized);
    }
}
