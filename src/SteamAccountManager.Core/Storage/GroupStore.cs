using System;
using System.Collections.Generic;
using System.Linq;
using SteamAccountManager.Core.Models;
using SteamAccountManager.Core.System;

namespace SteamAccountManager.Core.Storage;

public interface IGroupStore
{
    IReadOnlyList<Group> GetAll();
    Group Add(string name);
    void Rename(string id, string newName);
    void Delete(string id);
}

public sealed class GroupStore : IGroupStore
{
    private readonly string _path;
    private readonly IAtomicFile _atomicFile;
    private readonly List<Group> _groups;

    public GroupStore(string path, IAtomicFile atomicFile)
    {
        _path = path;
        _atomicFile = atomicFile;
        _groups = JsonFile.Load(path, () => new List<Group>());
    }

    // AsReadOnly prevents callers from casting back to List<Group> and mutating
    // the backing store behind Add/Rename/Delete (which would bypass persistence).
    public IReadOnlyList<Group> GetAll() => _groups.AsReadOnly();

    public Group Add(string name)
    {
        var group = new Group
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = name,
            SortOrder = _groups.Count == 0 ? 0 : _groups.Max(g => g.SortOrder) + 1,
        };

        _groups.Add(group);
        Save();
        return group;
    }

    public void Rename(string id, string newName)
    {
        var group = _groups.FirstOrDefault(g => g.Id == id);
        if (group is null)
        {
            return;
        }

        group.Name = newName;
        Save();
    }

    public void Delete(string id)
    {
        if (_groups.RemoveAll(g => g.Id == id) > 0)
        {
            Save();
        }
    }

    private void Save() => JsonFile.Save(_path, _groups, _atomicFile);
}
