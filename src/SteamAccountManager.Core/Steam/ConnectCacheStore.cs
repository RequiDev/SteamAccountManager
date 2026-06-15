using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SteamAccountManager.Core.System;
using ValveKeyValue;

namespace SteamAccountManager.Core.Steam;

/// <summary>
/// Preserves every account's Steam refresh-token across switches by maintaining the multi-account
/// <c>ConnectCache</c> in <c>local.vdf</c> — the same store Steam's own switcher uses.
///
/// local.vdf is a text VDF: <c>MachineUserConfigStore → Software → Valve → Steam → ConnectCache →
/// { key: hex(DPAPI token) }</c>, where key = <c>hex(CRC32(lowercase(accountName))) + "1"</c>.
/// Steam keeps several accounts' tokens here at once and silently logs in whichever
/// <c>AutoLoginUser</c> points to (if that token is still valid). We capture each account's entry
/// and re-inject the full set on every switch, so Steam never prunes one away. We only copy the
/// (DPAPI-encrypted) blobs; we never decrypt or read token contents.
/// </summary>
public interface IConnectCacheStore
{
    /// <summary>Reads live local.vdf ConnectCache entries into the persistent union (no write-back). Safe while Steam runs.</summary>
    void Capture(string localVdfPath);

    /// <summary>Captures live entries, then writes the full union back into local.vdf. Steam must be closed.</summary>
    void Merge(string localVdfPath);

    /// <summary>Whether a token is saved for this account (its ConnectCache key is in the union).</summary>
    bool HasToken(string accountName);

    /// <summary>
    /// Classifies how ready this account's cached token is for silent auto-login. Decrypts the token
    /// locally (DPAPI, current user) only to read its expiry; nothing is modified and nothing leaves
    /// the machine.
    /// </summary>
    TokenStatus GetStatus(string accountName);
}

public sealed class ConnectCacheStore : IConnectCacheStore
{
    private static readonly string[] CachePath = { "Software", "Valve", "Steam", "ConnectCache" };
    private const string RootName = "MachineUserConfigStore";
    private static readonly KVSerializer Serializer = KVSerializer.Create(KVSerializationFormat.KeyValues1Text);

    private readonly string _persistPath;
    private readonly IAtomicFile _atomicFile;
    private readonly Dictionary<string, string> _entries;

    public ConnectCacheStore(string persistPath, IAtomicFile atomicFile)
    {
        _persistPath = persistPath;
        _atomicFile = atomicFile;
        _entries = Load();
    }

    /// <summary>The ConnectCache key Steam uses for an account: hex(CRC32(lowercase(name))) + "1".</summary>
    public static string KeyFor(string accountName) => Crc32.HashHex(accountName.ToLowerInvariant()) + "1";

    public bool HasToken(string accountName) => _entries.ContainsKey(KeyFor(accountName));

    public TokenStatus GetStatus(string accountName)
    {
        if (!_entries.TryGetValue(KeyFor(accountName), out var hex) || string.IsNullOrEmpty(hex))
        {
            return TokenStatus.Missing;
        }

        byte[] decrypted;
        try
        {
            // Steam binds the token with DPAPI using the lowercased account name as entropy. If it
            // doesn't decrypt here, the blob came from another Windows user/machine (or is corrupt).
            var blob = Convert.FromHexString(hex);
            var entropy = Encoding.UTF8.GetBytes(accountName.ToLowerInvariant());
            decrypted = ProtectedData.Unprotect(blob, entropy, DataProtectionScope.CurrentUser);
        }
        catch (Exception)
        {
            return TokenStatus.ForeignMachine;
        }

        var jwt = Encoding.ASCII.GetString(decrypted);
        return IsUnexpiredJwt(jwt, DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            ? TokenStatus.Ready
            : TokenStatus.Expired;
    }

    /// <summary>True when <paramref name="jwt"/> is a JWT whose <c>exp</c> claim is after <paramref name="nowUnixSeconds"/>.</summary>
    internal static bool IsUnexpiredJwt(string jwt, long nowUnixSeconds)
    {
        var parts = jwt.Split('.');
        if (parts.Length < 2)
        {
            return false;
        }

        try
        {
            using var header = JsonDocument.Parse(Encoding.UTF8.GetString(Base64UrlDecode(parts[0])));
            if (!header.RootElement.TryGetProperty("typ", out var typ) ||
                !string.Equals(typ.GetString(), "JWT", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            using var payload = JsonDocument.Parse(Encoding.UTF8.GetString(Base64UrlDecode(parts[1])));
            return payload.RootElement.TryGetProperty("exp", out var expEl)
                && expEl.TryGetInt64(out var exp)
                && exp > nowUnixSeconds;
        }
        catch (Exception)
        {
            return false; // not a JWT we can read → treat as not ready
        }
    }

    private static byte[] Base64UrlDecode(string s)
    {
        s = s.Replace('-', '+').Replace('_', '/');
        switch (s.Length % 4)
        {
            case 2: s += "=="; break;
            case 3: s += "="; break;
        }

        return Convert.FromBase64String(s);
    }

    public void Capture(string localVdfPath)
    {
        var live = ReadCache(localVdfPath);
        var changed = false;
        foreach (var kv in live)
        {
            if (!_entries.TryGetValue(kv.Key, out var existing) || existing != kv.Value)
            {
                _entries[kv.Key] = kv.Value; // live entries are the freshest
                changed = true;
            }
        }

        if (changed)
        {
            Save();
        }
    }

    public void Merge(string localVdfPath)
    {
        Capture(localVdfPath);
        if (_entries.Count > 0)
        {
            WriteCache(localVdfPath, _entries);
        }
    }

    private static Dictionary<string, string> ReadCache(string localVdfPath)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!File.Exists(localVdfPath))
        {
            return result;
        }

        try
        {
            KVDocument doc;
            using (var fs = new FileStream(localVdfPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                doc = Serializer.Deserialize(fs);
            }

            // Navigate case-insensitively: Steam treats VDF keys case-insensitively and writes this
            // path's casing inconsistently across installs (e.g. "valve" vs "Valve"). A case-sensitive
            // lookup here silently returned nothing, making every account read as "no cached token".
            var node = doc.Root;
            foreach (var part in CachePath)
            {
                if (!VdfKeyValues.TryGetCI(node, part, out var child))
                {
                    return result;
                }

                node = child;
            }

            foreach (var kv in node)
            {
                result[kv.Key] = kv.Value.ToString();
            }
        }
        catch (Exception)
        {
            // Best-effort: an unreadable/locked/unexpected local.vdf must never break a switch.
            result.Clear();
        }

        return result;
    }

    private void WriteCache(string localVdfPath, IReadOnlyDictionary<string, string> entries)
    {
        var fileExists = File.Exists(localVdfPath);
        KVDocument? doc = null;
        if (fileExists)
        {
            try
            {
                using var fs = File.OpenRead(localVdfPath);
                doc = Serializer.Deserialize(fs);
            }
            catch (Exception)
            {
                doc = null;
            }
        }

        if (doc is not null)
        {
            // Graft the ConnectCache into the EXISTING document, creating only the missing path
            // nodes, so any other content under MachineUserConfigStore is preserved.
            var cache = GetOrCreateCacheNode(doc.Root);
            cache.Clear();
            foreach (var kv in entries)
            {
                cache[kv.Key] = kv.Value;
            }

            _atomicFile.Write(localVdfPath, stream => Serializer.Serialize(stream, doc));
        }
        else if (!fileExists)
        {
            // No local.vdf yet — create a minimal one containing just the ConnectCache.
            var root = BuildFresh(entries);
            _atomicFile.Write(localVdfPath, stream => Serializer.Serialize(stream, root, RootName));
        }

        // File exists but did not parse: leave it untouched rather than clobber a token file we
        // cannot safely read.
    }

    private static KVObject GetOrCreateCacheNode(KVObject root)
    {
        var node = root;
        foreach (var part in CachePath)
        {
            // Reuse an existing path node regardless of casing, so we never create a duplicate
            // differently-cased branch (e.g. a new "Valve" beside the install's existing "valve").
            if (!VdfKeyValues.TryGetCI(node, part, out var child))
            {
                child = KVObject.ListCollection();
                node[part] = child;
            }

            node = child;
        }

        return node;
    }

    private static KVObject BuildFresh(IReadOnlyDictionary<string, string> entries)
    {
        var cache = KVObject.ListCollection();
        foreach (var kv in entries)
        {
            cache[kv.Key] = kv.Value;
        }

        var steam = KVObject.ListCollection();
        steam["ConnectCache"] = cache;
        var valve = KVObject.ListCollection();
        valve["Steam"] = steam;
        var software = KVObject.ListCollection();
        software["Valve"] = valve;
        var root = KVObject.ListCollection();
        root["Software"] = software;
        return root;
    }

    private Dictionary<string, string> Load()
    {
        if (!File.Exists(_persistPath))
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        try
        {
            var json = File.ReadAllText(_persistPath);
            var loaded = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            return loaded is null
                ? new Dictionary<string, string>(StringComparer.Ordinal)
                : new Dictionary<string, string>(loaded, StringComparer.Ordinal);
        }
        catch (Exception)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }

    private void Save()
    {
        var json = JsonSerializer.Serialize(_entries);
        var bytes = Encoding.UTF8.GetBytes(json);
        _atomicFile.Write(_persistPath, stream => stream.Write(bytes, 0, bytes.Length));
    }
}
