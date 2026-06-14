using System;
using System.Collections.Generic;
using ValveKeyValue;

namespace SteamAccountManager.Core.Steam;

/// <summary>
/// Helpers around ValveKeyValue's case-sensitive (ordinal) key matching.
/// Steam treats keys case-insensitively, so reads scan case-insensitively and
/// writes overwrite the existing key using its on-disk casing (avoiding dup keys).
/// </summary>
internal static class VdfKeyValues
{
    public static bool TryGetCI(KVObject obj, string key, out KVObject value)
    {
        foreach (KeyValuePair<string, KVObject> child in obj)
        {
            if (string.Equals(child.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                value = child.Value;
                return true;
            }
        }

        value = null!;
        return false;
    }

    public static string? GetStringCI(KVObject obj, string key)
        => TryGetCI(obj, key, out var v) ? v.ToString() : null;

    public static bool GetBoolCI(KVObject obj, string key)
        => GetStringCI(obj, key) == "1";

    /// <summary>Sets a string value, reusing the existing key's casing if present.</summary>
    public static void SetStringPreservingCase(KVObject obj, string key, string value)
    {
        string? existingKey = null;
        foreach (KeyValuePair<string, KVObject> child in obj)
        {
            if (string.Equals(child.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                existingKey = child.Key;
                break;
            }
        }

        obj[existingKey ?? key] = value; // implicit string -> KVObject
    }
}
