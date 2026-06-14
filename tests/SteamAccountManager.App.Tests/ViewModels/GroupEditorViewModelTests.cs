using System;
using System.Linq;
using SteamAccountManager.App.Tests.Fakes;
using SteamAccountManager.App.ViewModels;
using Xunit;

namespace SteamAccountManager.App.Tests.ViewModels;

public class GroupEditorViewModelTests
{
    [Fact]
    public void Ctor_PopulatesMemberships_AndMarksCurrentGroups()
    {
        var groups = new FakeGroupManagementService();
        var work = groups.AddExisting("g1", "Work");
        groups.AddExisting("g2", "Personal");

        var sut = new GroupEditorViewModel(groups, "1", new[] { work.Id });

        Assert.Equal(2, sut.Memberships.Count);
        Assert.True(sut.Memberships.Single(m => m.GroupId == "g1").IsMember);
        Assert.False(sut.Memberships.Single(m => m.GroupId == "g2").IsMember);
    }

    [Fact]
    public void TogglingMembership_DelegatesToService()
    {
        var groups = new FakeGroupManagementService();
        groups.AddExisting("g1", "Work");
        var sut = new GroupEditorViewModel(groups, "1", Array.Empty<string>());

        sut.Memberships.Single(m => m.GroupId == "g1").IsMember = true;

        Assert.Equal(("1", "g1", true), groups.MembershipCalls.Single());
    }

    [Fact]
    public void AddGroupCommand_CreatesGroup_AndAddsMembershipRow()
    {
        var groups = new FakeGroupManagementService();
        var sut = new GroupEditorViewModel(groups, "1", Array.Empty<string>())
        {
            NewGroupName = "  New Crew  ",
        };

        sut.AddGroupCommand.Execute(null);

        Assert.Single(groups.GetGroups());
        Assert.Equal("New Crew", groups.GetGroups().Single().Name);
        Assert.Contains(sut.Memberships, m => m.Name == "New Crew");
        Assert.Equal("", sut.NewGroupName);
    }

    [Fact]
    public void RenameGroupCommand_DelegatesToService_AndUpdatesRow()
    {
        var groups = new FakeGroupManagementService();
        groups.AddExisting("g1", "Wrok");
        var sut = new GroupEditorViewModel(groups, "1", Array.Empty<string>());
        var row = sut.Memberships.Single(m => m.GroupId == "g1");
        row.Name = "Work"; // user typed a corrected name into the inline editor

        sut.RenameGroupCommand.Execute(row);

        Assert.Equal(("g1", "Work"), groups.RenameCalls.Single());
        Assert.Equal("Work", sut.Memberships.Single(m => m.GroupId == "g1").Name);
    }

    [Fact]
    public void DeleteGroupCommand_DelegatesToService_AndRemovesRow()
    {
        var groups = new FakeGroupManagementService();
        groups.AddExisting("g1", "Work");
        var sut = new GroupEditorViewModel(groups, "1", Array.Empty<string>());
        var row = sut.Memberships.Single(m => m.GroupId == "g1");

        sut.DeleteGroupCommand.Execute(row);

        Assert.Equal("g1", groups.DeleteCalls.Single());
        Assert.DoesNotContain(sut.Memberships, m => m.GroupId == "g1");
    }
}
