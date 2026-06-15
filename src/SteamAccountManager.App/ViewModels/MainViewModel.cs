using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SteamAccountManager.App.Services;
using SteamAccountManager.App.ViewModels;
using SteamAccountManager.Core.Avatars;
using SteamAccountManager.Core.Steam;

namespace SteamAccountManager.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private static readonly TimeSpan AddPollInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan AddTimeout = TimeSpan.FromMinutes(5);

    private readonly IAccountListService _accounts;
    private readonly IGroupManagementService _groups;
    private readonly IAccountSwitcher _switcher;
    private readonly IAvatarService _avatars;
    private readonly IAccountAddCoordinator _addCoordinator;

    public MainViewModel(
        IAccountListService accounts,
        IGroupManagementService groups,
        IAccountSwitcher switcher,
        IAvatarService avatars,
        IAccountAddCoordinator addCoordinator)
    {
        _accounts = accounts;
        _groups = groups;
        _switcher = switcher;
        _avatars = avatars;
        _addCoordinator = addCoordinator;
        _selectedGroupFilter = GroupFilterItem.All();
    }

    public ObservableCollection<AccountCardViewModel> Accounts { get; } = new();
    public ObservableCollection<AccountCardViewModel> FilteredAccounts { get; } = new();
    public ObservableCollection<GroupFilterItem> GroupFilters { get; } = new();

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string? StatusMessage { get; set; }

    /// <summary>Free-text filter matched against each account's display name and login name.</summary>
    [ObservableProperty]
    public partial string SearchText { get; set; } = "";

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    private GroupFilterItem _selectedGroupFilter;
    public GroupFilterItem SelectedGroupFilter
    {
        get => _selectedGroupFilter;
        set
        {
            // The TwoWay-bound ListBox writes null when its selected item leaves the
            // collection (e.g. during GroupFilters.Clear() on reload). Coerce that to
            // "All" so the selection — read by ApplyFilter and RebuildGroupFilters — is
            // never null.
            var coerced = value ?? GroupFilterItem.All();
            if (SetProperty(ref _selectedGroupFilter, coerced))
            {
                ApplyFilter();
            }
        }
    }

    public async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            // Record any newly-cached tokens from the live local.vdf before reading account status.
            _switcher.CaptureTokens();
            RebuildGroupFilters();
            RebuildAccounts();
            ApplyFilter();
            await LoadAvatarsAsync().ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private Task RefreshAsync() => LoadAsync();

    [RelayCommand]
    private async Task AddAccountAsync()
    {
        IsBusy = true;
        StatusMessage = "Steam will open for a one-time login. Sign in (Remember Password) to add the account...";
        try
        {
            var added = await _addCoordinator
                .BeginAddAndWaitAsync(AddPollInterval, AddTimeout, default)
                .ConfigureAwait(true);

            StatusMessage = added is null
                ? "No new account detected. You can refresh later once you have logged in."
                : $"Added {added.DisplayName}.";

            await LoadAsync().ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void RebuildGroupFilters()
    {
        // Snapshot the selection BEFORE clearing: clearing the bound collection makes the
        // ListBox push null into SelectedGroupFilter (coerced to "All"), so reading the field
        // afterwards would both risk a null deref and lose the user's real selection.
        var previous = _selectedGroupFilter;

        GroupFilters.Clear();
        GroupFilters.Add(GroupFilterItem.All());
        foreach (var g in _groups.GetGroups().OrderBy(g => g.SortOrder))
        {
            GroupFilters.Add(GroupFilterItem.ForGroup(g.Id, g.Name));
        }

        GroupFilters.Add(GroupFilterItem.Ungrouped());

        // Preserve selection when possible; otherwise default to "All".
        var match = GroupFilters.FirstOrDefault(f =>
            f.IsAll == previous.IsAll &&
            f.IsUngrouped == previous.IsUngrouped &&
            f.GroupId == previous.GroupId);
        _selectedGroupFilter = match ?? GroupFilterItem.All();
        OnPropertyChanged(nameof(SelectedGroupFilter));
    }

    private void RebuildAccounts()
    {
        foreach (var existing in Accounts)
        {
            existing.SwitchCompleted -= OnCardSwitchCompleted;
            existing.MetadataChanged -= OnCardMetadataChanged;
        }

        Accounts.Clear();
        foreach (var item in _accounts.GetAccounts())
        {
            var card = new AccountCardViewModel(item, _switcher, _groups);
            card.SwitchCompleted += OnCardSwitchCompleted;
            card.MetadataChanged += OnCardMetadataChanged;
            Accounts.Add(card);
        }
    }

    private async void OnCardSwitchCompleted(object? sender, string steamId64)
    {
        // After a switch, the active account changed — reload to update badges/avatars.
        await LoadAsync().ConfigureAwait(true);
    }

    private async void OnCardMetadataChanged(object? sender, string steamId64)
    {
        // After a label/notes edit, reload so DisplayName and filtering reflect the new metadata.
        await LoadAsync().ConfigureAwait(true);
    }

    private async Task LoadAvatarsAsync()
    {
        foreach (var card in Accounts)
        {
            var path = await _avatars.GetAvatarAsync(card.SteamId64).ConfigureAwait(true);
            card.AvatarPath = path;
        }
    }

    private void ApplyFilter()
    {
        FilteredAccounts.Clear();
        IEnumerable<AccountCardViewModel> source = Accounts;

        if (_selectedGroupFilter.IsUngrouped)
        {
            source = Accounts.Where(a => a.GroupIds.Count == 0);
        }
        else if (!_selectedGroupFilter.IsAll && _selectedGroupFilter.GroupId is { } gid)
        {
            source = Accounts.Where(a => a.GroupIds.Contains(gid));
        }

        // Combine (AND) with the free-text search over display name + login name.
        var search = SearchText?.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            source = source.Where(a =>
                a.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                a.AccountName.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var a in source)
        {
            FilteredAccounts.Add(a);
        }
    }
}
