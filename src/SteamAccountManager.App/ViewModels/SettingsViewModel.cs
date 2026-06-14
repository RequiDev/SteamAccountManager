using System;
using CommunityToolkit.Mvvm.ComponentModel;
using SteamAccountManager.Core.Models;
using SteamAccountManager.Core.Storage;
using SteamAccountManager.Core.System;

namespace SteamAccountManager.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly IAutostartService _autostart;
    private readonly ISettingsStore _settings;
    private bool _initializing;

    public SettingsViewModel(IAutostartService autostart, ISettingsStore settings)
    {
        _autostart = autostart;
        _settings = settings;
    }

    [ObservableProperty]
    public partial bool AutostartEnabled { get; set; }

    [ObservableProperty]
    public partial bool StartMinimized { get; set; }

    /// <summary>Loads the current state from the registry (autostart) and persisted settings.</summary>
    public void Initialize()
    {
        _initializing = true;
        try
        {
            var s = _settings.Load();
            AutostartEnabled = _autostart.IsEnabled();
            StartMinimized = s.StartMinimized;
        }
        finally
        {
            _initializing = false;
        }
    }

    partial void OnAutostartEnabledChanged(bool value)
    {
        if (_initializing)
        {
            return;
        }

        if (value)
        {
            _autostart.Enable(Environment.ProcessPath ?? AppContext.BaseDirectory);
        }
        else
        {
            _autostart.Disable();
        }

        Persist(s => s.AutostartEnabled = value);
    }

    partial void OnStartMinimizedChanged(bool value)
    {
        if (_initializing)
        {
            return;
        }

        Persist(s => s.StartMinimized = value);
    }

    private void Persist(Action<AppSettings> mutate)
    {
        var s = _settings.Load();
        mutate(s);
        _settings.Save(s);
    }
}
