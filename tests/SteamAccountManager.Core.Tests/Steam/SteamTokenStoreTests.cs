using System.IO;
using SteamAccountManager.Core.Steam;
using Xunit;

namespace SteamAccountManager.Core.Tests.Steam;

public class SteamTokenStoreTests
{
    [Fact]
    public void Capture_ThenRestore_RoundTripsLocalVdf()
    {
        using var tmp = new TestPaths();
        var localVdf = tmp.WriteFile("Steam/local.vdf", "TOKEN-A");
        var sut = new SteamTokenStore(tmp.File("tokens"));

        sut.Capture("76561198000000001", localVdf);
        Assert.True(sut.Has("76561198000000001"));

        // Simulate Steam overwriting local.vdf with a different account's token, then restore.
        File.WriteAllText(localVdf, "TOKEN-B");
        Assert.True(sut.Restore("76561198000000001", localVdf));
        Assert.Equal("TOKEN-A", File.ReadAllText(localVdf));
    }

    [Fact]
    public void Restore_ReturnsFalse_AndLeavesFile_WhenNoSavedToken()
    {
        using var tmp = new TestPaths();
        var localVdf = tmp.WriteFile("Steam/local.vdf", "LIVE");
        var sut = new SteamTokenStore(tmp.File("tokens"));

        Assert.False(sut.Restore("76561198000000099", localVdf));
        Assert.Equal("LIVE", File.ReadAllText(localVdf)); // untouched
    }

    [Fact]
    public void Capture_IsNoOp_WhenLocalVdfMissing()
    {
        using var tmp = new TestPaths();
        var sut = new SteamTokenStore(tmp.File("tokens"));

        sut.Capture("76561198000000001", tmp.File("nope/local.vdf")); // must not throw

        Assert.False(sut.Has("76561198000000001"));
    }
}
