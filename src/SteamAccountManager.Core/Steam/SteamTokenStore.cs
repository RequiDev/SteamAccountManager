using System;
using System.IO;

namespace SteamAccountManager.Core.Steam;

/// <summary>
/// Keeps a per-account copy of Steam's <c>local.vdf</c> (the refresh-token / ConnectCache store).
/// Steam's live <c>local.vdf</c> only holds the last-logged-in account's token, so silent
/// auto-login to any other account requires restoring that account's saved copy first.
///
/// The token blob is DPAPI-encrypted bound to (this Windows user + machine + account username),
/// so these copies are only ever restored on the same Windows user/machine that captured them —
/// which is exactly the local-switcher case. We never decrypt or read the token, only copy the file.
/// </summary>
public interface ISteamTokenStore
{
    bool Has(string steamId64);

    /// <summary>Saves the current live local.vdf as <paramref name="steamId64"/>'s token. Best-effort.</summary>
    void Capture(string steamId64, string localVdfPath);

    /// <summary>Restores <paramref name="steamId64"/>'s saved local.vdf into place. Returns whether it did.</summary>
    bool Restore(string steamId64, string localVdfPath);
}

public sealed class SteamTokenStore : ISteamTokenStore
{
    private readonly string _storeDirectory;

    public SteamTokenStore(string storeDirectory) => _storeDirectory = storeDirectory;

    private string PathFor(string steamId64) => Path.Combine(_storeDirectory, steamId64 + ".local.vdf");

    public bool Has(string steamId64) => File.Exists(PathFor(steamId64));

    public void Capture(string steamId64, string localVdfPath)
    {
        if (!File.Exists(localVdfPath))
        {
            return;
        }

        var dest = PathFor(steamId64);
        var temp = dest + ".tmp";
        try
        {
            Directory.CreateDirectory(_storeDirectory);
            // Steam may still hold local.vdf open; read with a shared handle. Write to a temp file
            // and rename, so a partial copy never truncates a previously-good snapshot.
            using (var source = new FileStream(localVdfPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var sink = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                source.CopyTo(sink);
            }

            File.Move(temp, dest, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best-effort: a locked/forbidden/partial local.vdf must never break a switch.
            TryDelete(temp);
        }
    }

    public bool Restore(string steamId64, string localVdfPath)
    {
        var saved = PathFor(steamId64);
        if (!File.Exists(saved))
        {
            return false;
        }

        try
        {
            var dir = Path.GetDirectoryName(Path.GetFullPath(localVdfPath));
            if (dir is not null)
            {
                Directory.CreateDirectory(dir);
            }

            File.Copy(saved, localVdfPath, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // best-effort cleanup
        }
    }
}
