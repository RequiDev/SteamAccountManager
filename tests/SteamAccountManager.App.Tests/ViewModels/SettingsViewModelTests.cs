using SteamAccountManager.App.Tests.Fakes;
using SteamAccountManager.App.ViewModels;
using SteamAccountManager.Core.Models;
using SteamAccountManager.Core.Storage;
using SteamAccountManager.Core.System;
using Xunit;

namespace SteamAccountManager.App.Tests.ViewModels;

public class SettingsViewModelTests
{
    private static SettingsViewModel Build(
        out FakeAutostartService autostart, out ISettingsStore settings, TestPaths tmp, bool autostartOn = false)
    {
        autostart = new FakeAutostartService();
        if (autostartOn) autostart.Enable("C:\\app.exe");
        settings = new SettingsStore(tmp.File("settings.json"), new AtomicFile());
        return new SettingsViewModel(autostart, settings);
    }

    [Fact]
    public void Initialize_ReflectsLiveAutostartAndPersistedStartMinimized()
    {
        using var tmp = new TestPaths();
        var store = new SettingsStore(tmp.File("settings.json"), new AtomicFile());
        store.Save(new AppSettings { StartMinimized = true });

        var auto = new FakeAutostartService();
        auto.Enable("C:\\app.exe");
        var sut = new SettingsViewModel(auto, store);

        sut.Initialize();

        Assert.True(sut.AutostartEnabled);
        Assert.True(sut.StartMinimized);
        // Loading initial state must NOT toggle autostart as a side-effect.
        Assert.Equal(1, auto.EnableCallCount); // only the pre-test Enable
        Assert.Equal(0, auto.DisableCallCount);
    }

    [Fact]
    public void SettingAutostart_EnablesRegistryAndPersists()
    {
        using var tmp = new TestPaths();
        var sut = Build(out var auto, out var settings, tmp);
        sut.Initialize();

        sut.AutostartEnabled = true;

        Assert.True(auto.IsEnabled());
        Assert.True(settings.Load().AutostartEnabled);
    }

    [Fact]
    public void ClearingAutostart_DisablesRegistryAndPersists()
    {
        using var tmp = new TestPaths();
        var sut = Build(out var auto, out var settings, tmp, autostartOn: true);
        sut.Initialize();

        sut.AutostartEnabled = false;

        Assert.False(auto.IsEnabled());
        Assert.False(settings.Load().AutostartEnabled);
    }

    [Fact]
    public void SettingStartMinimized_Persists()
    {
        using var tmp = new TestPaths();
        var sut = Build(out _, out var settings, tmp);
        sut.Initialize();

        sut.StartMinimized = true;

        Assert.True(settings.Load().StartMinimized);
    }
}
