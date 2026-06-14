using SteamAccountManager.App.ViewModels;
using Wpf.Ui.Controls;

namespace SteamAccountManager.App.Views;

public partial class GroupEditorWindow : FluentWindow
{
    public GroupEditorWindow(GroupEditorViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
