using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using SteamAccountManager.Core.Updates;
using Xunit;

namespace SteamAccountManager.Core.Tests.Updates;

public class GitHubUpdateServiceTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(_responder(request));
    }

    private static GitHubUpdateService Service(Func<HttpRequestMessage, HttpResponseMessage> responder)
        => new(new HttpClient(new StubHandler(responder)));

    private static HttpResponseMessage Json(string body)
        => new(HttpStatusCode.OK) { Content = new StringContent(body) };

    private static string ReleaseJson(string tag, params string[] assetNames)
    {
        var assets = new List<string>();
        foreach (var n in assetNames)
        {
            assets.Add($"{{\"name\":\"{n}\",\"browser_download_url\":\"https://example.test/{n}\"}}");
        }

        return $"{{\"tag_name\":\"{tag}\",\"html_url\":\"https://example.test/release\",\"assets\":[{string.Join(",", assets)}]}}";
    }

    [Fact]
    public async Task CheckForUpdate_ReturnsInfo_WhenRemoteIsNewer()
    {
        var svc = Service(_ => Json(ReleaseJson("v0.3.0", "SteamAccountManager.App.exe")));

        var info = await svc.CheckForUpdateAsync(new Version(0, 2, 2, 0));

        Assert.NotNull(info);
        Assert.Equal(new Version(0, 3, 0), info!.Version);
        Assert.Equal("v0.3.0", info.TagName);
        Assert.Equal("https://example.test/SteamAccountManager.App.exe", info.ExeDownloadUrl);
        Assert.Equal("https://example.test/release", info.ReleaseUrl);
    }

    [Theory]
    [InlineData("v0.2.2")] // identical to current
    [InlineData("v0.2.1")] // older than current
    public async Task CheckForUpdate_ReturnsNull_WhenNotNewer(string tag)
    {
        var svc = Service(_ => Json(ReleaseJson(tag, "SteamAccountManager.App.exe")));

        Assert.Null(await svc.CheckForUpdateAsync(new Version(0, 2, 2, 0)));
    }

    [Fact]
    public async Task CheckForUpdate_ReturnsNull_WhenExeAssetMissing()
    {
        var svc = Service(_ => Json(ReleaseJson("v0.3.0", "SomethingElse.zip")));

        Assert.Null(await svc.CheckForUpdateAsync(new Version(0, 2, 2, 0)));
    }

    [Fact]
    public async Task Download_WritesAssetToDisk_CreatingDirectories()
    {
        var payload = new byte[] { 1, 2, 3, 4, 5 };
        var svc = Service(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(payload) });
        using var tmp = new TestPaths();
        var dest = tmp.File("download/app.exe");
        var info = new UpdateInfo(new Version(0, 3, 0), "v0.3.0", "https://example.test/app.exe", "https://example.test/release");

        await svc.DownloadAsync(info, dest);

        Assert.Equal(payload, File.ReadAllBytes(dest));
    }

    [Theory]
    [InlineData("v0.3.0", 0, 3, 0)]
    [InlineData("0.3", 0, 3, 0)]
    [InlineData("v1.2.3-rc1", 1, 2, 3)]
    [InlineData("1.2.3+build7", 1, 2, 3)]
    public void TryParseVersion_NormalizesTags(string tag, int major, int minor, int build)
    {
        Assert.True(GitHubUpdateService.TryParseVersion(tag, out var v));
        Assert.Equal(new Version(major, minor, build), v);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-version")]
    public void TryParseVersion_FailsOnGarbage(string tag)
    {
        Assert.False(GitHubUpdateService.TryParseVersion(tag, out _));
    }
}
