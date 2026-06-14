using System.IO;
using System.Threading.Tasks;
using SteamAccountManager.Core.Avatars;
using SteamAccountManager.Core.Tests.Fakes;
using Xunit;

namespace SteamAccountManager.Core.Tests.Avatars;

public class AvatarServiceTests
{
    [Fact]
    public async Task GetAvatarAsync_DownloadsCachesAndReturnsPath()
    {
        using var tmp = new TestPaths();
        var fetcher = new FakeAvatarFetcher(new byte[] { 1, 2, 3 });
        var sut = new AvatarService(tmp.File("avatars"), fetcher);

        var path = await sut.GetAvatarAsync("76561198000000001");

        Assert.NotNull(path);
        Assert.True(File.Exists(path));
        Assert.Equal(new byte[] { 1, 2, 3 }, await File.ReadAllBytesAsync(path!));
    }

    [Fact]
    public async Task GetAvatarAsync_UsesCache_OnSecondCall()
    {
        using var tmp = new TestPaths();
        var fetcher = new FakeAvatarFetcher(new byte[] { 9 });
        var sut = new AvatarService(tmp.File("avatars"), fetcher);

        await sut.GetAvatarAsync("76561198000000001");
        await sut.GetAvatarAsync("76561198000000001");

        Assert.Equal(1, fetcher.CallCount); // second call served from disk cache
    }

    [Fact]
    public async Task GetAvatarAsync_ReturnsNull_WhenFetchFails()
    {
        using var tmp = new TestPaths();
        var sut = new AvatarService(tmp.File("avatars"), new FakeAvatarFetcher(null));

        Assert.Null(await sut.GetAvatarAsync("76561198000000001"));
    }
}
