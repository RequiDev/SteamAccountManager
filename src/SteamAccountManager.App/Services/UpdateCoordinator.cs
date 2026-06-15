using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using SteamAccountManager.App.Infrastructure;
using SteamAccountManager.Core.Updates;

namespace SteamAccountManager.App.Services;

public interface IUpdateCoordinator
{
    /// <summary>
    /// Checks for a newer release and, if the user accepts, downloads it and restarts into it.
    /// <paramref name="userInitiated"/> surfaces "up to date" / error dialogs; the silent startup
    /// check stays quiet unless an update is actually available.
    /// </summary>
    Task CheckAsync(bool userInitiated);
}

public sealed class UpdateCoordinator : IUpdateCoordinator
{
    private const string TempExeName = "SteamAccountManager.update.exe";

    private readonly IUpdateService _update;
    private int _busy;

    public UpdateCoordinator(IUpdateService update) => _update = update;

    public async Task CheckAsync(bool userInitiated)
    {
        // Never run two checks at once (startup check + a manual click, say).
        if (Interlocked.Exchange(ref _busy, 1) == 1)
        {
            return;
        }

        try
        {
            var current = typeof(App).Assembly.GetName().Version ?? new Version(0, 0, 0, 0);

            UpdateInfo? info;
            try
            {
                info = await _update.CheckForUpdateAsync(current).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                CrashLogger.Log(ex, "UpdateCheck");
                if (userInitiated)
                {
                    Show($"Couldn't check for updates right now.{NL}{NL}{ex.Message}", MessageBoxImage.Warning);
                }

                return;
            }

            if (info is null)
            {
                if (userInitiated)
                {
                    Show("You're running the latest version.", MessageBoxImage.Information);
                }

                return;
            }

            var proceed = Ask(
                $"Version {info.Version.ToString(3)} is available (you have {current.ToString(3)}).{NL}{NL}" +
                "Download it and restart now?");
            if (!proceed)
            {
                return;
            }

            var dest = Path.Combine(Path.GetTempPath(), TempExeName);
            try
            {
                await _update.DownloadAsync(info, dest).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                CrashLogger.Log(ex, "UpdateDownload");
                Show($"The update failed to download.{NL}{NL}{ex.Message}", MessageBoxImage.Warning);
                return;
            }

            var self = Environment.ProcessPath;
            if (string.IsNullOrEmpty(self))
            {
                Show("Couldn't locate the application to update.", MessageBoxImage.Warning);
                return;
            }

            RestartIntoUpdate(dest, self!);
        }
        finally
        {
            Interlocked.Exchange(ref _busy, 0);
        }
    }

    private static void RestartIntoUpdate(string newExe, string installedExe)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            var psi = new ProcessStartInfo(newExe) { UseShellExecute = true };
            psi.ArgumentList.Add(UpdateApplier.ApplyArg);
            psi.ArgumentList.Add(installedExe);
            psi.ArgumentList.Add(Environment.ProcessId.ToString());
            Process.Start(psi);
            Application.Current.Shutdown();
        });
    }

    private static readonly string NL = Environment.NewLine;

    private static bool Ask(string message)
        => Application.Current.Dispatcher.Invoke(() =>
            MessageBox.Show(message, "Update available", MessageBoxButton.YesNo, MessageBoxImage.Question)
                == MessageBoxResult.Yes);

    private static void Show(string message, MessageBoxImage icon)
        => Application.Current.Dispatcher.Invoke(() =>
            MessageBox.Show(message, "Steam Account Manager", MessageBoxButton.OK, icon));
}
