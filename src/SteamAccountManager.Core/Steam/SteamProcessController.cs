using System;
using System.Diagnostics;
using System.Threading;
using SteamAccountManager.Core.Models;

namespace SteamAccountManager.Core.Steam;

public interface ISteamProcessController
{
    bool IsSteamRunning();

    /// <summary>Requests a graceful shutdown and waits. Returns true once Steam is gone.</summary>
    bool ShutdownAndWait(TimeSpan timeout);

    void Launch(string? arguments = null);
}

public sealed class SteamProcessController : ISteamProcessController
{
    private const string SteamProcessName = "steam"; // steam.exe -> "steam"
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(250);

    private readonly ISteamLocator _locator;

    public SteamProcessController(ISteamLocator locator) => _locator = locator;

    public bool IsSteamRunning()
        => Process.GetProcessesByName(SteamProcessName).Length > 0;

    public bool ShutdownAndWait(TimeSpan timeout)
    {
        if (!IsSteamRunning())
        {
            return true;
        }

        var paths = _locator.Locate();
        if (paths is null)
        {
            return false;
        }

        using (Process.Start(new ProcessStartInfo(paths.ExecutablePath, "-shutdown") { UseShellExecute = false }))
        {
        }

        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (!IsSteamRunning())
            {
                return true;
            }

            Thread.Sleep(PollInterval);
        }

        return !IsSteamRunning();
    }

    public void Launch(string? arguments = null)
    {
        var paths = _locator.Locate() ?? throw new SteamNotFoundException();
        using (Process.Start(new ProcessStartInfo(paths.ExecutablePath, arguments ?? "") { UseShellExecute = true }))
        {
        }
    }
}
