namespace SteamAccountManager.Core.Models;

public sealed class AppSettings
{
    public bool AutostartEnabled { get; set; }
    public bool StartMinimized { get; set; }

    /// <summary>Check GitHub for a newer release on launch. Enabled by default (also for existing
    /// settings files that predate this option, since the initializer wins when the key is absent).</summary>
    public bool CheckForUpdatesOnStartup { get; set; } = true;
}
