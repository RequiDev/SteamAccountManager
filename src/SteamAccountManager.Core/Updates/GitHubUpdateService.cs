using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SteamAccountManager.Core.Updates;

/// <summary>
/// Resolves updates from a public GitHub repository's "latest release" and downloads its
/// executable asset. Read-only against the GitHub REST API; requires no credentials.
/// </summary>
public sealed class GitHubUpdateService : IUpdateService
{
    private readonly HttpClient _http;
    private readonly string _latestReleaseUrl;
    private readonly string _assetName;

    public GitHubUpdateService(
        HttpClient http,
        string repo = "RequiDev/SteamAccountManager",
        string assetName = "SteamAccountManager.App.exe")
    {
        _http = http;
        _latestReleaseUrl = $"https://api.github.com/repos/{repo}/releases/latest";
        _assetName = assetName;
    }

    public async Task<UpdateInfo?> CheckForUpdateAsync(Version currentVersion, CancellationToken ct = default)
    {
        using var resp = await _http.GetAsync(_latestReleaseUrl, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();

        await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
        var root = doc.RootElement;

        var tag = root.TryGetProperty("tag_name", out var tagEl) ? tagEl.GetString() : null;
        if (!TryParseVersion(tag, out var remote) || remote <= Normalize(currentVersion))
        {
            return null;
        }

        var exeUrl = FindAssetUrl(root);
        if (string.IsNullOrWhiteSpace(exeUrl))
        {
            return null;
        }

        var releaseUrl = root.TryGetProperty("html_url", out var urlEl) ? urlEl.GetString() ?? "" : "";
        return new UpdateInfo(remote, tag!, exeUrl!, releaseUrl);
    }

    public async Task DownloadAsync(UpdateInfo update, string destinationPath, CancellationToken ct = default)
    {
        using var resp = await _http
            .GetAsync(update.ExeDownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();

        var dir = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        await using var src = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        await using var dst = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await src.CopyToAsync(dst, ct).ConfigureAwait(false);
    }

    private string? FindAssetUrl(JsonElement root)
    {
        if (!root.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var asset in assets.EnumerateArray())
        {
            var name = asset.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : null;
            if (string.Equals(name, _assetName, StringComparison.OrdinalIgnoreCase))
            {
                return asset.TryGetProperty("browser_download_url", out var u) ? u.GetString() : null;
            }
        }

        return null;
    }

    /// <summary>Parses a release tag like "v0.3.0" / "0.3" into a normalized major.minor.build Version.</summary>
    internal static bool TryParseVersion(string? tag, out Version version)
    {
        version = new Version(0, 0, 0);
        if (string.IsNullOrWhiteSpace(tag))
        {
            return false;
        }

        var s = tag.Trim();
        if (s.StartsWith("v", StringComparison.OrdinalIgnoreCase))
        {
            s = s.Substring(1);
        }

        // Drop any pre-release / build-metadata suffix (e.g. "1.2.3-rc1", "1.2.3+abc").
        var cut = s.IndexOfAny(new[] { '-', '+' });
        if (cut >= 0)
        {
            s = s.Substring(0, cut);
        }

        if (!Version.TryParse(s, out var parsed))
        {
            return false;
        }

        version = Normalize(parsed);
        return true;
    }

    /// <summary>Collapses to major.minor.build (treating unset components as 0) for stable comparison.</summary>
    private static Version Normalize(Version v)
        => new(Math.Max(0, v.Major), Math.Max(0, v.Minor), Math.Max(0, v.Build));
}
