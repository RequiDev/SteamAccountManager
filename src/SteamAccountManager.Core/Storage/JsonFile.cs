using System;
using System.IO;
using System.Text;
using System.Text.Json;
using SteamAccountManager.Core.System;

namespace SteamAccountManager.Core.Storage;

internal static class JsonFile
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static T Load<T>(string path, Func<T> defaultFactory)
    {
        if (!File.Exists(path))
        {
            return defaultFactory();
        }

        var json = File.ReadAllText(path);
        if (string.IsNullOrWhiteSpace(json))
        {
            return defaultFactory();
        }

        return JsonSerializer.Deserialize<T>(json, Options) ?? defaultFactory();
    }

    public static void Save<T>(string path, T value, IAtomicFile atomicFile)
    {
        var json = JsonSerializer.Serialize(value, Options);
        var bytes = Encoding.UTF8.GetBytes(json); // GetBytes never emits a BOM
        atomicFile.Write(path, stream => stream.Write(bytes, 0, bytes.Length));
    }
}
