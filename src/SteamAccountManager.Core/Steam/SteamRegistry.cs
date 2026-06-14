using SteamAccountManager.Core.System;

namespace SteamAccountManager.Core.Steam;

public interface ISteamRegistry
{
    string? GetAutoLoginUser();
    void SetAutoLoginUser(string accountName);
    void ClearAutoLoginUser();
    bool GetRememberPassword();
    void SetRememberPassword(bool value);

    /// <summary>
    /// SteamID64 of the account Steam is currently logged into, or null when not logged in
    /// (sitting at the login screen). Read from ActiveProcess\ActiveUser (a SteamID3/account id).
    /// </summary>
    string? GetActiveAccountSteamId64();
}

public sealed class SteamRegistry : ISteamRegistry
{
    private const string SteamKey = @"Software\Valve\Steam";
    private const string ActiveProcessKey = @"Software\Valve\Steam\ActiveProcess";
    private const string AutoLoginUserValue = "AutoLoginUser";
    private const string RememberPasswordValue = "RememberPassword";
    private const long SteamId64Base = 76561197960265728L; // SteamID64 = base + account id (SteamID3)

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

    public string? GetActiveAccountSteamId64()
    {
        var accountId = _registry.GetDword(RegistryHiveSelector.CurrentUser, ActiveProcessKey, "ActiveUser");
        if (accountId is null or 0)
        {
            return null;
        }

        // ActiveUser is a 32-bit account id; cast through uint so account ids above 2^31 stay positive.
        return (SteamId64Base + (uint)accountId.Value).ToString();
    }
}
