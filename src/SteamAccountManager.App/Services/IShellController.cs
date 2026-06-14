namespace SteamAccountManager.App.Services;

/// <summary>
/// App-level window/lifecycle operations the tray VM triggers. Implemented in App.xaml.cs / MainWindow
/// so the viewmodel never touches WPF types and stays unit-testable.
/// </summary>
public interface IShellController
{
    /// <summary>Shows and activates the main window.</summary>
    void ShowMainWindow();

    /// <summary>Really exits the app (bypassing close-to-tray).</summary>
    void ExitApplication();

    /// <summary>Reloads the dashboard after an external change (e.g. a switch from the tray menu).</summary>
    void RefreshDashboard();
}
