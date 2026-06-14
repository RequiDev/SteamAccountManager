using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SteamAccountManager.App.Models;
using SteamAccountManager.App.Services;
using SteamAccountManager.Core.Steam;

namespace SteamAccountManager.App.ViewModels;

public partial class AccountCardViewModel : ObservableObject
{
    private readonly IAccountSwitcher _switcher;
    private readonly IGroupManagementService _groups;
    private readonly string _fallbackName;
    private string? _currentLabel;
    private string? _currentNotes;

    public AccountCardViewModel(AccountListItem item, IAccountSwitcher switcher, IGroupManagementService groups)
    {
        _switcher = switcher;
        _groups = groups;
        SteamId64 = item.SteamId64;
        AccountName = item.AccountName;
        DisplayName = item.DisplayName;
        GroupIds = item.GroupIds;
        IsActive = item.IsActive;
        LastLogin = item.LastLogin;
        _currentLabel = item.CustomLabel;
        _currentNotes = item.Notes;
        // Name to show when no custom label is set (mirrors AccountListItem.DisplayName).
        _fallbackName = !string.IsNullOrWhiteSpace(item.PersonaName) ? item.PersonaName : item.AccountName;
    }

    public string SteamId64 { get; }
    public string AccountName { get; }
    public IReadOnlyList<string> GroupIds { get; }
    public DateTimeOffset? LastLogin { get; }

    /// <summary>Raised after a successful switch with the SteamID64; parent VM refreshes on this.</summary>
    public event EventHandler<string>? SwitchCompleted;

    /// <summary>Raised after the label/notes are saved; parent VM refreshes on this (mirrors SwitchCompleted).</summary>
    public event EventHandler<string>? MetadataChanged;

    [ObservableProperty]
    public partial string DisplayName { get; set; }

    [ObservableProperty]
    public partial bool IsActive { get; set; }

    [ObservableProperty]
    public partial string? AvatarPath { get; set; }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    public partial bool IsEditing { get; set; }

    [ObservableProperty]
    public partial string? EditableLabel { get; set; }

    [ObservableProperty]
    public partial string? EditableNotes { get; set; }

    [RelayCommand]
    private async Task SwitchAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            // SwitchTo is blocking (shuts down Steam, writes files, relaunches): off the UI thread.
            await Task.Run(() => _switcher.SwitchTo(SteamId64)).ConfigureAwait(true);
            SwitchCompleted?.Invoke(this, SteamId64);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void BeginEdit()
    {
        EditableLabel = _currentLabel;
        EditableNotes = _currentNotes;
        IsEditing = true;
    }

    [RelayCommand]
    private void SaveLabel()
    {
        var label = string.IsNullOrWhiteSpace(EditableLabel) ? null : EditableLabel;
        var notes = string.IsNullOrWhiteSpace(EditableNotes) ? null : EditableNotes;

        _groups.SetLabelAndNotes(SteamId64, label, notes);

        // Reflect locally so DisplayName updates immediately; the parent also refreshes from disk.
        _currentLabel = label;
        _currentNotes = notes;
        DisplayName = label ?? _fallbackName; // cleared label reverts to persona/account name
        IsEditing = false;
        MetadataChanged?.Invoke(this, SteamId64);
    }

    [RelayCommand]
    private void CancelEdit()
    {
        EditableLabel = _currentLabel;
        EditableNotes = _currentNotes;
        IsEditing = false;
    }
}
