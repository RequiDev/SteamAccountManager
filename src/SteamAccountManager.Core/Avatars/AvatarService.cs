using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SteamAccountManager.Core.Avatars;

public interface IAvatarFetcher
{
    /// <summary>Returns avatar image bytes for a SteamID64, or null if unavailable.</summary>
    Task<byte[]?> FetchAsync(string steamId64, CancellationToken ct = default);
}

public interface IAvatarService
{
    /// <summary>Returns a local cached avatar file path, or null if none could be obtained.</summary>
    Task<string?> GetAvatarAsync(string steamId64, CancellationToken ct = default);
}

public sealed class AvatarService : IAvatarService
{
    private readonly string _cacheDirectory;
    private readonly IAvatarFetcher _fetcher;

    public AvatarService(string cacheDirectory, IAvatarFetcher fetcher)
    {
        _cacheDirectory = cacheDirectory;
        _fetcher = fetcher;
    }

    private string CachePath(string steamId64) => Path.Combine(_cacheDirectory, steamId64 + ".jpg");

    public async Task<string?> GetAvatarAsync(string steamId64, CancellationToken ct = default)
    {
        var cached = CachePath(steamId64);
        if (File.Exists(cached))
        {
            return cached;
        }

        var bytes = await _fetcher.FetchAsync(steamId64, ct).ConfigureAwait(false);
        if (bytes is null || bytes.Length == 0)
        {
            return null;
        }

        Directory.CreateDirectory(_cacheDirectory);
        await File.WriteAllBytesAsync(cached, bytes, ct).ConfigureAwait(false);
        return cached;
    }
}
