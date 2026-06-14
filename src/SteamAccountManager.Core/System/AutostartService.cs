namespace SteamAccountManager.Core.System;

public interface IAutostartService
{
    bool IsEnabled();
    void Enable(string executablePath);
    void Disable();
}

public sealed class AutostartService : IAutostartService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "SteamAccountManager";

    private readonly IWindowsRegistry _registry;

    public AutostartService(IWindowsRegistry registry) => _registry = registry;

    public bool IsEnabled()
        => !string.IsNullOrEmpty(
            _registry.GetString(RegistryHiveSelector.CurrentUser, RunKey, ValueName));

    public void Enable(string executablePath)
        => _registry.SetString(
            RegistryHiveSelector.CurrentUser, RunKey, ValueName, $"\"{executablePath}\" --minimized");

    public void Disable()
        => _registry.DeleteValue(RegistryHiveSelector.CurrentUser, RunKey, ValueName);
}
