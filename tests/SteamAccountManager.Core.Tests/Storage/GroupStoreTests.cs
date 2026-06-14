using System.Linq;
using SteamAccountManager.Core.Storage;
using SteamAccountManager.Core.System;
using Xunit;

namespace SteamAccountManager.Core.Tests.Storage;

public class GroupStoreTests
{
    [Fact]
    public void Add_AssignsIdAndIncrementingSortOrder_AndPersists()
    {
        using var tmp = new TestPaths();
        var path = tmp.File("groups.json");
        var atomic = new AtomicFile();

        var store = new GroupStore(path, atomic);
        var main = store.Add("Main");
        var smurfs = store.Add("Smurfs");

        Assert.False(string.IsNullOrWhiteSpace(main.Id));
        Assert.NotEqual(main.Id, smurfs.Id);
        Assert.Equal(0, main.SortOrder);
        Assert.Equal(1, smurfs.SortOrder);

        var reloaded = new GroupStore(path, atomic);
        Assert.Equal(2, reloaded.GetAll().Count);
    }

    [Fact]
    public void Rename_ChangesName()
    {
        using var tmp = new TestPaths();
        var store = new GroupStore(tmp.File("groups.json"), new AtomicFile());
        var g = store.Add("Old");

        store.Rename(g.Id, "New");

        Assert.Equal("New", store.GetAll().Single().Name);
    }

    [Fact]
    public void Delete_RemovesGroup()
    {
        using var tmp = new TestPaths();
        var store = new GroupStore(tmp.File("groups.json"), new AtomicFile());
        var g = store.Add("Temp");

        store.Delete(g.Id);

        Assert.Empty(store.GetAll());
    }
}
