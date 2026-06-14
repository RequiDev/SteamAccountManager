using System.Linq;
using SteamAccountManager.App.Services;
using SteamAccountManager.Core.Models;
using SteamAccountManager.Core.Storage;
using SteamAccountManager.Core.System;
using Xunit;

namespace SteamAccountManager.App.Tests.Services;

public class GroupManagementServiceTests
{
    private static (GroupManagementService Sut, IGroupStore Groups, IAccountMetadataStore Meta) Build(TestPaths tmp)
    {
        var atomic = new AtomicFile();
        var groups = new GroupStore(tmp.File("groups.json"), atomic);
        var meta = new AccountMetadataStore(tmp.File("metadata.json"), atomic);
        return (new GroupManagementService(groups, meta), groups, meta);
    }

    [Fact]
    public void CreateGroup_AddsToStore()
    {
        using var tmp = new TestPaths();
        var (sut, _, _) = Build(tmp);

        var g = sut.CreateGroup("Work");

        Assert.Equal("Work", g.Name);
        Assert.Single(sut.GetGroups());
    }

    [Fact]
    public void RenameGroup_UpdatesName()
    {
        using var tmp = new TestPaths();
        var (sut, _, _) = Build(tmp);
        var g = sut.CreateGroup("Wrok");

        sut.RenameGroup(g.Id, "Work");

        Assert.Equal("Work", sut.GetGroups().Single().Name);
    }

    [Fact]
    public void DeleteGroup_RemovesGroup_AndStripsIdFromAllAccountMetadata()
    {
        using var tmp = new TestPaths();
        var (sut, _, meta) = Build(tmp);
        var g = sut.CreateGroup("Work");
        var other = sut.CreateGroup("Personal");

        meta.Upsert(new AccountMetadata { SteamId64 = "1", GroupIds = { g.Id, other.Id } });
        meta.Upsert(new AccountMetadata { SteamId64 = "2", GroupIds = { g.Id } });

        sut.DeleteGroup(g.Id);

        Assert.DoesNotContain(sut.GetGroups(), x => x.Id == g.Id);
        Assert.Equal(new[] { other.Id }, meta.Get("1").GroupIds);
        Assert.Empty(meta.Get("2").GroupIds);
    }

    [Fact]
    public void SetMembership_AddsAndRemovesGroupId_AndPersists()
    {
        using var tmp = new TestPaths();
        var (sut, _, meta) = Build(tmp);
        var g = sut.CreateGroup("Work");

        sut.SetMembership("1", g.Id, isMember: true);
        Assert.Contains(g.Id, meta.Get("1").GroupIds);

        sut.SetMembership("1", g.Id, isMember: true); // idempotent
        Assert.Single(meta.Get("1").GroupIds);

        sut.SetMembership("1", g.Id, isMember: false);
        Assert.Empty(meta.Get("1").GroupIds);
    }

    [Fact]
    public void SetLabelAndNotes_UpdatesLabelAndNotes_AndPreservesGroupIds()
    {
        using var tmp = new TestPaths();
        var (sut, _, meta) = Build(tmp);
        var g = sut.CreateGroup("Work");
        meta.Upsert(new AccountMetadata { SteamId64 = "1", GroupIds = { g.Id } });

        sut.SetLabelAndNotes("1", "Main account", "smurf for ranked");

        var stored = meta.Get("1");
        Assert.Equal("Main account", stored.CustomLabel);
        Assert.Equal("smurf for ranked", stored.Notes);
        Assert.Equal(new[] { g.Id }, stored.GroupIds); // membership preserved
    }

    [Fact]
    public void SetLabelAndNotes_AllowsClearingToNull()
    {
        using var tmp = new TestPaths();
        var (sut, _, meta) = Build(tmp);
        meta.Upsert(new AccountMetadata { SteamId64 = "1", CustomLabel = "Old", Notes = "Old notes" });

        sut.SetLabelAndNotes("1", null, null);

        var stored = meta.Get("1");
        Assert.Null(stored.CustomLabel);
        Assert.Null(stored.Notes);
    }
}
