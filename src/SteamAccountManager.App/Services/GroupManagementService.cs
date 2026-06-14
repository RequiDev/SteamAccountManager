using System.Collections.Generic;
using System.Linq;
using SteamAccountManager.Core.Models;
using SteamAccountManager.Core.Storage;

namespace SteamAccountManager.App.Services;

/// <summary>
/// Account-metadata coordination across <see cref="IGroupStore"/> and <see cref="IAccountMetadataStore"/>:
/// group CRUD plus the cross-store cleanup Core defers (deleting a group strips its id from every account's
/// metadata), account group-membership toggles, and per-account custom label + notes updates.
/// </summary>
public interface IGroupManagementService
{
    IReadOnlyList<Group> GetGroups();
    Group CreateGroup(string name);
    void RenameGroup(string id, string newName);
    void DeleteGroup(string id);
    void SetMembership(string steamId64, string groupId, bool isMember);

    /// <summary>Updates an account's custom label and notes, preserving its group memberships.</summary>
    void SetLabelAndNotes(string steamId64, string? label, string? notes);
}

public sealed class GroupManagementService : IGroupManagementService
{
    private readonly IGroupStore _groups;
    private readonly IAccountMetadataStore _metadata;

    public GroupManagementService(IGroupStore groups, IAccountMetadataStore metadata)
    {
        _groups = groups;
        _metadata = metadata;
    }

    public IReadOnlyList<Group> GetGroups() => _groups.GetAll();

    public Group CreateGroup(string name) => _groups.Add(name);

    public void RenameGroup(string id, string newName) => _groups.Rename(id, newName);

    public void DeleteGroup(string id)
    {
        _groups.Delete(id);

        // Cross-store cleanup: remove the deleted group id from every account that referenced it.
        foreach (var meta in _metadata.GetAll().Values.ToList())
        {
            if (meta.GroupIds.Remove(id))
            {
                _metadata.Upsert(meta);
            }
        }
    }

    public void SetMembership(string steamId64, string groupId, bool isMember)
    {
        var meta = _metadata.Get(steamId64);
        var contains = meta.GroupIds.Contains(groupId);

        if (isMember && !contains)
        {
            meta.GroupIds.Add(groupId);
        }
        else if (!isMember && contains)
        {
            meta.GroupIds.Remove(groupId);
        }
        else
        {
            return; // no change
        }

        _metadata.Upsert(meta);
    }

    public void SetLabelAndNotes(string steamId64, string? label, string? notes)
    {
        // Get never returns null (fresh default when absent); GroupIds is carried through unchanged.
        var meta = _metadata.Get(steamId64);
        meta.CustomLabel = label;
        meta.Notes = notes;
        _metadata.Upsert(meta);
    }
}
