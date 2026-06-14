using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SteamAccountManager.App.Services;

namespace SteamAccountManager.App.ViewModels;

public partial class GroupMembershipItem : ObservableObject
{
    public required string GroupId { get; init; }

    /// <summary>Editable group name (two-way bound to the inline rename TextBox).</summary>
    [ObservableProperty]
    public partial string Name { get; set; }

    [ObservableProperty]
    public partial bool IsMember { get; set; }
}

/// <summary>Edits one account's group memberships and lets the user create/rename/delete groups.</summary>
public partial class GroupEditorViewModel : ObservableObject
{
    private readonly IGroupManagementService _groups;
    private readonly string _steamId64;

    public GroupEditorViewModel(IGroupManagementService groups, string steamId64, IReadOnlyList<string> currentGroupIds)
    {
        _groups = groups;
        _steamId64 = steamId64;
        var current = currentGroupIds.ToHashSet();
        foreach (var g in _groups.GetGroups().OrderBy(g => g.SortOrder))
        {
            var item = new GroupMembershipItem { GroupId = g.Id, Name = g.Name, IsMember = current.Contains(g.Id) };
            item.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(GroupMembershipItem.IsMember))
                {
                    _groups.SetMembership(_steamId64, item.GroupId, item.IsMember);
                }
            };
            Memberships.Add(item);
        }
    }

    public ObservableCollection<GroupMembershipItem> Memberships { get; } = new();

    [ObservableProperty]
    public partial string NewGroupName { get; set; } = "";

    [RelayCommand]
    private void AddGroup()
    {
        if (string.IsNullOrWhiteSpace(NewGroupName))
        {
            return;
        }

        var g = _groups.CreateGroup(NewGroupName.Trim());
        Memberships.Add(new GroupMembershipItem { GroupId = g.Id, Name = g.Name, IsMember = false });
        NewGroupName = "";
    }

    [RelayCommand]
    private void RenameGroup(GroupMembershipItem? item)
    {
        if (item is null || string.IsNullOrWhiteSpace(item.Name))
        {
            return;
        }

        var newName = item.Name.Trim();
        _groups.RenameGroup(item.GroupId, newName);
        item.Name = newName; // normalize the displayed value
    }

    [RelayCommand]
    private void DeleteGroup(GroupMembershipItem? item)
    {
        if (item is null)
        {
            return;
        }

        _groups.DeleteGroup(item.GroupId);
        Memberships.Remove(item);
    }
}
