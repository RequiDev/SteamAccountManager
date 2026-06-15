using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using SteamAccountManager.App.Infrastructure;
using SteamAccountManager.Core.Updates;
using MessageBox = Wpf.Ui.Controls.MessageBox;
using MessageBoxResult = Wpf.Ui.Controls.MessageBoxResult;

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
    private static readonly string NL = Environment.NewLine;

    private readonly IUpdateService _update;
    private int _busy;

    public UpdateCoordinator(IUpdateService update) => _update = update;

    // Runs on the UI thread (invoked from the tray command or OnStartup); awaits keep it there, so
    // the Fluent dialogs below are shown on the UI thread without marshalling.
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
                info = await _update.CheckForUpdateAsync(current);
            }
            catch (Exception ex)
            {
                CrashLogger.Log(ex, "UpdateCheck");
                if (userInitiated)
                {
                    await InfoAsync("Update check failed", $"Couldn't check for updates right now.{NL}{NL}{ex.Message}");
                }

                return;
            }

            if (info is null)
            {
                if (userInitiated)
                {
                    await InfoAsync("No updates", "You're running the latest version.");
                }

                return;
            }

            var proceed = await ConfirmAsync(
                "Update available",
                $"Version {info.Version.ToString(3)} is available (you have {current.ToString(3)}).{NL}{NL}" +
                "Download it and restart now?");
            if (!proceed)
            {
                return;
            }

            var dest = Path.Combine(Path.GetTempPath(), TempExeName);
            try
            {
                await _update.DownloadAsync(info, dest);
            }
            catch (Exception ex)
            {
                CrashLogger.Log(ex, "UpdateDownload");
                await InfoAsync("Update failed", $"The update failed to download.{NL}{NL}{ex.Message}");
                return;
            }

            var self = Environment.ProcessPath;
            if (string.IsNullOrEmpty(self))
            {
                await InfoAsync("Update failed", "Couldn't locate the application to update.");
                return;
            }

            var psi = new ProcessStartInfo(dest) { UseShellExecute = true };
            psi.ArgumentList.Add(UpdateApplier.ApplyArg);
            psi.ArgumentList.Add(self!);
            psi.ArgumentList.Add(Environment.ProcessId.ToString());
            Process.Start(psi);
            Application.Current.Shutdown();
        }
        finally
        {
            Interlocked.Exchange(ref _busy, 0);
        }
    }

    private static async Task<bool> ConfirmAsync(string title, string content)
    {
        var box = NewBox(title, content, closeText: "Later");
        box.PrimaryButtonText = "Update now";
        return await box.ShowDialogAsync() == MessageBoxResult.Primary;
    }

    private static async Task InfoAsync(string title, string content)
    {
        await NewBox(title, content, closeText: "OK").ShowDialogAsync();
    }

    private static MessageBox NewBox(string title, string content, string closeText)
    {
        var box = new MessageBox
        {
            Title = title,
            Content = content,
            CloseButtonText = closeText,
            MaxWidth = 460,
        };

        // Parent on the main window only when it's actually on screen; otherwise the dialog stands
        // alone (centered), which also avoids the closing tray popup ever owning — and dismissing — it.
        var owner = Application.Current?.MainWindow;
        if (owner is { IsLoaded: true } && owner.IsVisible)
        {
            box.Owner = owner;
        }

        return box;
    }
}
