using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace SteamAccountManager.Core.Avatars;

/// <summary>Keyless avatar lookup via the public Steam Community profile XML endpoint.</summary>
public sealed class SteamCommunityAvatarFetcher : IAvatarFetcher
{
    private readonly HttpClient _http;

    public SteamCommunityAvatarFetcher(HttpClient http) => _http = http;

    public async Task<byte[]?> FetchAsync(string steamId64, CancellationToken ct = default)
    {
        try
        {
            var xml = await _http
                .GetStringAsync($"https://steamcommunity.com/profiles/{steamId64}?xml=1", ct)
                .ConfigureAwait(false);

            var url = ParseAvatarUrl(xml);
            if (url is null)
            {
                return null;
            }

            return await _http.GetByteArrayAsync(url, ct).ConfigureAwait(false);
        }
        catch (HttpRequestException)
        {
            return null; // offline / private / transient — caller falls back to a default avatar
        }
    }

    internal static string? ParseAvatarUrl(string xml)
    {
        try
        {
            var url = XDocument.Parse(xml).Root?.Element("avatarFull")?.Value?.Trim();
            return string.IsNullOrWhiteSpace(url) ? null : url;
        }
        catch (XmlException)
        {
            return null;
        }
    }
}
