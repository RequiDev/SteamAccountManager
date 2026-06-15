using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace SteamAccountManager.App.Infrastructure;

/// <summary>
/// Performs the self-replacing step of an update. A single-file exe can't overwrite itself while
/// running, so the freshly-downloaded exe is relaunched with <see cref="ApplyArg"/>; that instance
/// waits for the old process to exit, copies itself over the installed exe, and relaunches it.
/// </summary>
internal static class UpdateApplier
{
    public const string ApplyArg = "--apply-update";

    /// <summary>Parses <c>--apply-update &lt;targetExePath&gt; &lt;pid&gt;</c> from the args.</summary>
    public static bool TryParse(string[] args, out string targetPath, out int pid)
    {
        targetPath = "";
        pid = 0;

        var idx = Array.FindIndex(args, a => string.Equals(a, ApplyArg, StringComparison.OrdinalIgnoreCase));
        if (idx < 0 || idx + 2 >= args.Length)
        {
            return false;
        }

        if (!int.TryParse(args[idx + 2], out pid))
        {
            return false;
        }

        targetPath = args[idx + 1];
        return !string.IsNullOrWhiteSpace(targetPath);
    }

    /// <summary>
    /// If invoked as the update helper, replaces the installed exe and relaunches it.
    /// Returns true when it handled an apply-update request (the caller should then exit).
    /// </summary>
    public static bool TryApply(string[] args)
    {
        if (!TryParse(args, out var target, out var pid))
        {
            return false;
        }

        try
        {
            WaitForExit(pid, TimeSpan.FromSeconds(30));

            var self = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(self) && !string.Equals(self, target, StringComparison.OrdinalIgnoreCase))
            {
                CopyWithRetry(self!, target, attempts: 40, delay: TimeSpan.FromMilliseconds(500));
            }
        }
        catch (Exception ex)
        {
            // Copy failed — the installed exe is untouched, so the relaunch below restores the
            // existing version rather than leaving the user with no running app.
            CrashLogger.Log(ex, "ApplyUpdate");
        }

        // Always relaunch: on success the user gets the new version, on failure the old one back.
        try
        {
            if (File.Exists(target))
            {
                Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
            }
        }
        catch (Exception ex)
        {
            CrashLogger.Log(ex, "ApplyUpdateRelaunch");
        }

        return true;
    }

    private static void WaitForExit(int pid, TimeSpan timeout)
    {
        try
        {
            using var p = Process.GetProcessById(pid);
            p.WaitForExit((int)timeout.TotalMilliseconds);
        }
        catch (ArgumentException)
        {
            // Process already gone — nothing to wait for.
        }
    }

    private static void CopyWithRetry(string source, string dest, int attempts, TimeSpan delay)
    {
        for (var i = 0; ; i++)
        {
            try
            {
                File.Copy(source, dest, overwrite: true);
                return;
            }
            catch (Exception ex) when ((ex is IOException or UnauthorizedAccessException) && i < attempts)
            {
                Thread.Sleep(delay); // installed exe may still be locked for a moment after exit
            }
        }
    }
}
