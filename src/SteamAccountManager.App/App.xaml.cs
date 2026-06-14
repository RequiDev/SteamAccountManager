using System;
using System.Threading;
using System.Windows;
using H.NotifyIcon;
using Microsoft.Extensions.DependencyInjection;
using SteamAccountManager.App.Infrastructure;
using SteamAccountManager.App.Services;
using SteamAccountManager.App.ViewModels;
using SteamAccountManager.Core.Avatars;
using SteamAccountManager.Core.Steam;
using SteamAccountManager.Core.Storage;
using SteamAccountManager.Core.System;

namespace SteamAccountManager.App;

public partial class App : Application, IShellController
{
    private const string MutexName = "SteamAccountManager.SingleInstance.Mutex";
    private const string ShowEventName = "SteamAccountManager.SingleInstance.ShowEvent";

    private Mutex? _mutex;
    private EventWaitHandle? _showEvent;
    private RegisteredWaitHandle? _registeredWait;
    private ServiceProvider? _provider;
    private TaskbarIcon? _notifyIcon;
    private MainWindow? _mainWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // ---- Single instance ----
        _mutex = new Mutex(initiallyOwned: true, MutexName, out var isFirstInstance);
        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);

        if (!isFirstInstance)
        {
            _showEvent.Set();   // ask the running instance to surface its window
            Shutdown();
            return;
        }

        _registeredWait = ThreadPool.RegisterWaitForSingleObject(
            _showEvent,
            (_, _) => Dispatcher.Invoke(ShowMainWindow),
            state: null,
            millisecondsTimeOutInterval: Timeout.Infinite,
            executeOnlyOnce: false);

        // ---- DI ----
        var services = new ServiceCollection();
        ConfigureServices(services);
        _provider = services.BuildServiceProvider();

        _provider.GetRequiredService<IAppPaths>().EnsureCreated();

        // ---- Tray ----
        var trayViewModel = _provider.GetRequiredService<TrayViewModel>();
        _notifyIcon = (TaskbarIcon)FindResource("NotifyIcon");
        _notifyIcon.DataContext = trayViewModel;
        _notifyIcon.LeftClickCommand = trayViewModel.ShowWindowCommand;
        _notifyIcon.ForceCreate();

        // ---- Main window ----
        _mainWindow = _provider.GetRequiredService<MainWindow>();

        var startMinimized = ShouldStartMinimized(e.Args);
        if (!startMinimized)
        {
            ShowMainWindow();
        }

        // Kick off the first data load and tray menu build.
        _ = _mainWindow.ViewModel.LoadAsync();
        _provider.GetRequiredService<TrayViewModel>().Rebuild();
    }

    private bool ShouldStartMinimized(string[] args)
    {
        foreach (var arg in args)
        {
            if (string.Equals(arg, "--minimized", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        // Also honor the persisted StartMinimized preference.
        var settings = _provider!.GetRequiredService<ISettingsStore>().Load();
        return settings.StartMinimized;
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // App paths first; everything else derives from it.
        services.AddSingleton<IAppPaths>(_ => new AppPaths());

        // Core/System primitives.
        services.AddSingleton<IAtomicFile, AtomicFile>();
        services.AddSingleton<IWindowsRegistry, WindowsRegistry>();
        services.AddSingleton<IAutostartService, AutostartService>();

        // Core/Steam.
        services.AddSingleton<ISteamLocator, SteamLocator>();
        services.AddSingleton<ILoginUsersStore, LoginUsersStore>();
        services.AddSingleton<ISteamRegistry, SteamRegistry>();
        services.AddSingleton<ISteamProcessController, SteamProcessController>();
        services.AddSingleton<IBackupService>(sp =>
            new BackupService(sp.GetRequiredService<IAppPaths>().BackupsDirectory));
        services.AddSingleton<IAccountSwitcher, AccountSwitcher>();

        // Core/Storage (factory lambdas pass the AppPaths file paths + the singleton IAtomicFile).
        services.AddSingleton<IAccountMetadataStore>(sp =>
            new AccountMetadataStore(sp.GetRequiredService<IAppPaths>().MetadataFile, sp.GetRequiredService<IAtomicFile>()));
        services.AddSingleton<IGroupStore>(sp =>
            new GroupStore(sp.GetRequiredService<IAppPaths>().GroupsFile, sp.GetRequiredService<IAtomicFile>()));
        services.AddSingleton<ISettingsStore>(sp =>
            new SettingsStore(sp.GetRequiredService<IAppPaths>().SettingsFile, sp.GetRequiredService<IAtomicFile>()));

        // Core/Avatars — typed HttpClient for the fetcher; AvatarService caches to the avatar dir.
        services.AddHttpClient<IAvatarFetcher, SteamCommunityAvatarFetcher>(c =>
        {
            c.Timeout = TimeSpan.FromSeconds(15);
            c.DefaultRequestHeaders.UserAgent.ParseAdd("SteamAccountManager/1.0");
        });
        services.AddSingleton<IAvatarService>(sp =>
            new AvatarService(
                sp.GetRequiredService<IAppPaths>().AvatarCacheDirectory,
                sp.GetRequiredService<IAvatarFetcher>()));

        // App services.
        services.AddSingleton<IAccountListService, AccountListService>();
        services.AddSingleton<IGroupManagementService, GroupManagementService>();
        services.AddSingleton<IAccountAddCoordinator, AccountAddCoordinator>();
        services.AddSingleton<IShellController>(_ => this);

        // ViewModels + window.
        services.AddSingleton<TrayViewModel>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<MainWindow>();
    }

    // ---- IShellController ----
    public void ShowMainWindow()
    {
        _mainWindow ??= _provider!.GetRequiredService<MainWindow>();
        _mainWindow.Show();
        if (_mainWindow.WindowState == WindowState.Minimized)
        {
            _mainWindow.WindowState = WindowState.Normal;
        }

        _mainWindow.Activate();
        _mainWindow.Topmost = true;
        _mainWindow.Topmost = false;
    }

    public void ExitApplication()
    {
        // Route through the window so its OnClosing knows this is a real exit.
        if (_mainWindow is not null)
        {
            _mainWindow.RequestExit();
        }
        else
        {
            Shutdown();
        }
    }

    public void RefreshDashboard()
    {
        // Called after a tray-initiated switch (already on the UI thread) so the dashboard's
        // active badge and account list reflect the new state.
        if (_mainWindow is not null)
        {
            _ = _mainWindow.ViewModel.LoadAsync();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _registeredWait?.Unregister(null);
        _notifyIcon?.Dispose();
        _provider?.Dispose();
        _showEvent?.Dispose();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
