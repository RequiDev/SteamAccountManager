using SteamAccountManager.App.ViewModels;
using Wpf.Ui.Controls;

namespace SteamAccountManager.App.Views;

public partial class SettingsWindow : FluentWindow
{
    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        viewModel.Initialize();
        DataContext = viewModel;
    }
}
