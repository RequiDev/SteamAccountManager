using System;
using System.ComponentModel;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using SteamAccountManager.App.Services;
using SteamAccountManager.App.ViewModels;
using SteamAccountManager.App.Views;
using SteamAccountManager.Core.Storage;
using Wpf.Ui.Controls;

namespace SteamAccountManager.App;

public partial class MainWindow : FluentWindow
{
    private readonly ISettingsStore _settings;
    private bool _reallyExiting;

    public MainWindow(MainViewModel viewModel, ISettingsStore settings)
    {
        _settings = settings;
        InitializeComponent();
        DataContext = viewModel;
        ViewModel = viewModel;
        RestoreWindowSize();
    }

    public MainViewModel ViewModel { get; }

    public void RequestExit()
    {
        _reallyExiting = true;
        global::System.Windows.Application.Current.Shutdown();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // Capture the size whether we're hiding to tray or really exiting.
        SaveWindowSize();

        if (!_reallyExiting)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnClosing(e);
    }

    private void RestoreWindowSize()
    {
        var s = _settings.Load();
        if (s.WindowWidth >= 200)
        {
            Width = s.WindowWidth;
        }

        if (s.WindowHeight >= 200)
        {
            Height = s.WindowHeight;
        }

        if (s.WindowMaximized)
        {
            WindowState = WindowState.Maximized;
        }
    }

    private void SaveWindowSize()
    {
        var s = _settings.Load();
        s.WindowMaximized = WindowState == WindowState.Maximized;

        // When normal, read the live size; otherwise RestoreBounds holds the size to return to.
        var bounds = WindowState == WindowState.Normal
            ? new Rect(Left, Top, ActualWidth, ActualHeight)
            : RestoreBounds;
        if (bounds.Width >= 200 && bounds.Height >= 200)
        {
            s.WindowWidth = bounds.Width;
            s.WindowHeight = bounds.Height;
        }

        _settings.Save(s);
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
