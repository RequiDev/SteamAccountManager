using System;
using System.IO;

namespace SteamAccountManager.App.Infrastructure;

/// <summary>
/// Writes unhandled-exception details to a log file. Deliberately self-contained (no DI,
/// no other services) so it works even when the app is in a broken state, and never throws.
/// </summary>
internal static class CrashLogger
{
    private static string DefaultDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SteamAccountManager",
        "logs");

    /// <summary>Logs an exception. Returns the written file path, or null if logging failed.</summary>
    public static string? Log(Exception exception, string source, string? directory = null)
    {
        try
        {
            var dir = directory ?? DefaultDirectory;
            Directory.CreateDirectory(dir);
            var file = Path.Combine(dir, $"crash-{DateTime.UtcNow:yyyyMMdd-HHmmss-fffffff}.log");
            File.WriteAllText(
                file,
                $"[{DateTimeOffset.Now:O}] {source}{Environment.NewLine}{exception}{Environment.NewLine}");
            return file;
        }
        catch
        {
            // A crash logger must never crash the crash handler.
            return null;
        }
    }
}
