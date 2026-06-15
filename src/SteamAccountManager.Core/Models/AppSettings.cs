namespace SteamAccountManager.Core.Models;

public sealed class AppSettings
{
    public bool AutostartEnabled { get; set; }
    public bool StartMinimized { get; set; }

    /// <summary>Check GitHub for a newer release on launch. Enabled by default (also for existing
    /// settings files that predate this option, since the initializer wins when the key is absent).</summary>
    public bool CheckForUpdatesOnStartup { get; set; } = true;

    /// <summary>Persisted main-window size; defaults match the window's design size for first run.</summary>
    public double WindowWidth { get; set; } = 980;
    public double WindowHeight { get; set; } = 640;

    /// <summary>Whether the main window was maximized when last closed.</summary>
    public bool WindowMaximized { get; set; }
}
