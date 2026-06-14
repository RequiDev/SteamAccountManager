using SteamAccountManager.Core.Models;
using SteamAccountManager.Core.Storage;
using SteamAccountManager.Core.System;
using Xunit;

namespace SteamAccountManager.Core.Tests.Storage;

public class AccountMetadataStoreTests
{
    [Fact]
    public void Get_ReturnsEmptyMetadata_WhenUnknown()
    {
        using var tmp = new TestPaths();
        var sut = new AccountMetadataStore(tmp.File("metadata.json"), new AtomicFile());

        var meta = sut.Get("76561198000000001");

        Assert.Equal("76561198000000001", meta.SteamId64);
        Assert.Null(meta.CustomLabel);
        Assert.Empty(meta.GroupIds);
    }

    [Fact]
    public void Upsert_Persists_AndSurvivesReload()
    {
        using var tmp = new TestPaths();
        var path = tmp.File("metadata.json");
        var atomic = new AtomicFile();

        var store = new AccountMetadataStore(path, atomic);
        store.Upsert(new AccountMetadata
        {
            SteamId64 = "76561198000000001",
            CustomLabel = "Main",
            GroupIds = { "g1", "g2" },
        });

        var reloaded = new AccountMetadataStore(path, atomic);
        var meta = reloaded.Get("76561198000000001");

        Assert.Equal("Main", meta.CustomLabel);
        Assert.Equal(new[] { "g1", "g2" }, meta.GroupIds);
    }
}
