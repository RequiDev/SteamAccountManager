using System;
using System.IO;
using SteamAccountManager.Core.Models;
using SteamAccountManager.Core.System;

namespace SteamAccountManager.Core.Steam;

public interface ISteamLocator
{
    SteamPaths? Locate();
}

public sealed class SteamLocator : ISteamLocator
{
    private readonly IWindowsRegistry _registry;

    public SteamLocator(IWindowsRegistry registry) => _registry = registry;

    public SteamPaths? Locate()
    {
        var install =
            _registry.GetString(RegistryHiveSelector.CurrentUser, @"Software\Valve\Steam", "SteamPath")
            ?? _registry.GetString(RegistryHiveSelector.LocalMachine, @"SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath")
            ?? _registry.GetString(RegistryHiveSelector.LocalMachine, @"SOFTWARE\Valve\Steam", "InstallPath");

        if (string.IsNullOrWhiteSpace(install))
        {
            return null;
        }

        install = install.Replace('/', '\\').TrimEnd('\\');
        var configDir = Path.Combine(install, "config");

        // local.vdf (the token store) always lives under LocalAppData, independent of where
        // Steam itself is installed.
        var localVdf = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Steam",
            "local.vdf");

        return new SteamPaths(
            InstallDirectory: install,
            ExecutablePath: Path.Combine(install, "steam.exe"),
            ConfigDirectory: configDir,
            LoginUsersPath: Path.Combine(configDir, "loginusers.vdf"),
            LocalVdfPath: localVdf);
    }
}
