using SteamAccountManager.Core.System;

namespace SteamAccountManager.Core.Steam;

public interface ISteamRegistry
{
    string? GetAutoLoginUser();
    void SetAutoLoginUser(string accountName);
    void ClearAutoLoginUser();
    bool GetRememberPassword();
    void SetRememberPassword(bool value);
}

public sealed class SteamRegistry : ISteamRegistry
{
    private const string SteamKey = @"Software\Valve\Steam";
    private const string AutoLoginUserValue = "AutoLoginUser";
    private const string RememberPasswordValue = "RememberPassword";

    private readonly IWindowsRegistry _registry;

    public SteamRegistry(IWindowsRegistry registry) => _registry = registry;

    public string? GetAutoLoginUser()
        => _registry.GetString(RegistryHiveSelector.CurrentUser, SteamKey, AutoLoginUserValue);

    public void SetAutoLoginUser(string accountName)
        => _registry.SetString(RegistryHiveSelector.CurrentUser, SteamKey, AutoLoginUserValue, accountName);

    public void ClearAutoLoginUser()
        => _registry.SetString(RegistryHiveSelector.CurrentUser, SteamKey, AutoLoginUserValue, "");

    public bool GetRememberPassword()
        => _registry.GetDword(RegistryHiveSelector.CurrentUser, SteamKey, RememberPasswordValue) == 1;

    public void SetRememberPassword(bool value)
        => _registry.SetDword(RegistryHiveSelector.CurrentUser, SteamKey, RememberPasswordValue, value ? 1 : 0);
}
