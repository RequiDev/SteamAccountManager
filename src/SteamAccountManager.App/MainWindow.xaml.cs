using System;
using System.ComponentModel;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using SteamAccountManager.App.Services;
using SteamAccountManager.App.ViewModels;
using SteamAccountManager.App.Views;
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

    public void RequestExit()
    {
        _reallyExiting = true;
        global::System.Windows.Application.Current.Shutdown();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_reallyExiting)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnClosing(e);
    }

    private void OnSettingsClick(object sender, RoutedEventArgs e)
    {
        var window = App.Services.GetRequiredService<SettingsWindow>();
        window.Owner = this;
        window.ShowDialog();
    }

    private async void OnEditGroupsClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: AccountCardViewModel card })
        {
            return;
        }

        var groups = App.Services.GetRequiredService<IGroupManagementService>();
        var vm = new GroupEditorViewModel(groups, card.SteamId64, card.GroupIds);
        var window = new GroupEditorWindow(vm) { Owner = this };
        window.ShowDialog();

        // Group memberships may have changed → refresh dashboard and tray.
        await ViewModel.RefreshCommand.ExecuteAsync(null);
        App.Services.GetRequiredService<TrayViewModel>().Rebuild();
    }
}
