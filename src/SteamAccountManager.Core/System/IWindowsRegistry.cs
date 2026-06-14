namespace SteamAccountManager.Core.System;

public enum RegistryHiveSelector
{
    CurrentUser,
    LocalMachine,
}

/// <summary>Thin abstraction over the Windows registry so consumers are testable.</summary>
public interface IWindowsRegistry
{
    string? GetString(RegistryHiveSelector hive, string subKey, string name);
    int? GetDword(RegistryHiveSelector hive, string subKey, string name);
    void SetString(RegistryHiveSelector hive, string subKey, string name, string value);
    void SetDword(RegistryHiveSelector hive, string subKey, string name, int value);
    void DeleteValue(RegistryHiveSelector hive, string subKey, string name);
}
