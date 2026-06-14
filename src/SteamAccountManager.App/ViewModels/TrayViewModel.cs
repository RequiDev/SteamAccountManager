using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SteamAccountManager.App.Models;
using SteamAccountManager.App.Services;
using SteamAccountManager.Core.Steam;
using SteamAccountManager.Core.Storage;
using SteamAccountManager.Core.System;

namespace SteamAccountManager.App.ViewModels;

public partial class TrayViewModel : ObservableObject
{
    private readonly IAccountListService _accounts;
    private readonly IGroupManagementService _groups;
    private readonly IAccountSwitcher _switcher;
    private readonly IAutostartService _autostart;
    private readonly ISettingsStore _settings;
    private readonly IShellController _shell;

    public TrayViewModel(
        IAccountListService accounts,
        IGroupManagementService groups,
        IAccountSwitcher switcher,
        IAutostartService autostart,
        ISettingsStore settings,
        IShellController shell)
    {
        _accounts = accounts;
        _groups = groups;
        _switcher = switcher;
        _autostart = autostart;
        _settings = settings;
        _shell = shell;
        IsAutostartEnabled = _autostart.IsEnabled();
    }

    public ObservableCollection<TrayGroupItem> Groups { get; } = new();

    [ObservableProperty]
    public partial bool IsAutostartEnabled { get; set; }

    /// <summary>Rebuilds the grouped account menu structure from the latest account/group data.</summary>
    public void Rebuild()
    {
        var accounts = _accounts.GetAccounts();
        var groups = _groups.GetGroups().OrderBy(g => g.SortOrder).ToList();

        Groups.Clear();

        foreach (var group in groups)
        {
            var members = accounts
                .Where(a => a.GroupIds.Contains(group.Id))
                .Select(a => new TrayAccountItem(a.SteamId64, a.DisplayName, a.IsActive))
                .ToList();

            if (members.Count > 0)
            {
                Groups.Add(new TrayGroupItem(group.Name, members));
            }
        }

        var ungrouped = accounts
            .Where(a => a.GroupIds.Count == 0)
            .Select(a => new TrayAccountItem(a.SteamId64, a.DisplayName, a.IsActive))
            .ToList();

        if (ungrouped.Count > 0)
        {
            Groups.Add(new TrayGroupItem("Ungrouped", ungrouped));
        }
    }

    [RelayCommand]
    private void ShowWindow() => _shell.ShowMainWindow();

    [RelayCommand]
    private void Exit() => _shell.ExitApplication();

    [RelayCommand]
    private void ToggleAutostart()
    {
        if (IsAutostartEnabled)
        {
            _autostart.Disable();
            IsAutostartEnabled = false;
        }
        else
        {
            var exe = Environment.ProcessPath ?? AppContext.BaseDirectory;
            _autostart.Enable(exe);
            IsAutostartEnabled = true;
        }

        var s = _settings.Load();
        s.AutostartEnabled = IsAutostartEnabled;
        _settings.Save(s);
    }

    [RelayCommand]
    private async Task SwitchAccountAsync(string? steamId64)
    {
        if (string.IsNullOrEmpty(steamId64))
        {
            return;
        }

        await Task.Run(() => _switcher.SwitchTo(steamId64)).ConfigureAwait(true);
        Rebuild();
        _shell.RefreshDashboard(); // keep the open dashboard's active badge in sync
    }
}
