using System;
using System.Collections.Generic;
using System.Linq;
using SteamAccountManager.App.Services;
using SteamAccountManager.Core.Models;

namespace SteamAccountManager.App.Tests.Fakes;

public sealed class FakeGroupManagementService : IGroupManagementService
{
    private readonly List<Group> _groups = new();

    public IReadOnlyList<Group> GetGroups() => _groups;

    public Group CreateGroup(string name)
    {
        var g = new Group { Id = Guid.NewGuid().ToString("N"), Name = name, SortOrder = _groups.Count };
        _groups.Add(g);
        return g;
    }

    public List<(string Id, string NewName)> RenameCalls { get; } = new();
    public void RenameGroup(string id, string newName)
    {
        RenameCalls.Add((id, newName));
        var g = _groups.FirstOrDefault(x => x.Id == id);
        if (g is not null) g.Name = newName;
    }

    public List<string> DeleteCalls { get; } = new();
    public void DeleteGroup(string id)
    {
        DeleteCalls.Add(id);
        _groups.RemoveAll(x => x.Id == id);
    }

    public List<(string SteamId64, string GroupId, bool IsMember)> MembershipCalls { get; } = new();
    public void SetMembership(string steamId64, string groupId, bool isMember)
        => MembershipCalls.Add((steamId64, groupId, isMember));

    public List<(string SteamId64, string? Label, string? Notes)> LabelNotesCalls { get; } = new();
    public void SetLabelAndNotes(string steamId64, string? label, string? notes)
        => LabelNotesCalls.Add((steamId64, label, notes));

    // Test seam to preload groups with known ids.
    public Group AddExisting(string id, string name)
    {
        var g = new Group { Id = id, Name = name, SortOrder = _groups.Count };
        _groups.Add(g);
        return g;
    }
}
