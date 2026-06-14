using System.ComponentModel;
using SteamAccountManager.App.ViewModels;
using Wpf.Ui.Controls;

namespace SteamAccountManager.App;

public partial class MainWindow : FluentWindow
{
    private bool _reallyExiting;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        ViewModel = viewModel;
    }

    public MainViewModel ViewModel { get; }

    /// <summary>Called by the shell when the user chooses Exit from the tray.</summary>
    public void RequestExit()
    {
        _reallyExiting = true;
        global::System.Windows.Application.Current.Shutdown();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_reallyExiting)
        {
            // Close (X) hides to tray; app keeps running (ShutdownMode=OnExplicitShutdown).
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnClosing(e);
    }
}
