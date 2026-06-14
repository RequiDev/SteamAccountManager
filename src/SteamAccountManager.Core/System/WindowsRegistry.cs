using System;
using Microsoft.Win32;

namespace SteamAccountManager.Core.System;

public sealed class WindowsRegistry : IWindowsRegistry
{
    public string? GetString(RegistryHiveSelector hive, string subKey, string name)
    {
        using var key = BaseKey(hive).OpenSubKey(subKey);
        return key?.GetValue(name) as string;
    }

    public int? GetDword(RegistryHiveSelector hive, string subKey, string name)
    {
        using var key = BaseKey(hive).OpenSubKey(subKey);
        var value = key?.GetValue(name);
        return value is null ? null : Convert.ToInt32(value);
    }

    public void SetString(RegistryHiveSelector hive, string subKey, string name, string value)
    {
        using var key = BaseKey(hive).CreateSubKey(subKey, writable: true);
        key.SetValue(name, value, RegistryValueKind.String);
    }

    public void SetDword(RegistryHiveSelector hive, string subKey, string name, int value)
    {
        using var key = BaseKey(hive).CreateSubKey(subKey, writable: true);
        key.SetValue(name, value, RegistryValueKind.DWord);
    }

    public void DeleteValue(RegistryHiveSelector hive, string subKey, string name)
    {
        using var key = BaseKey(hive).OpenSubKey(subKey, writable: true);
        key?.DeleteValue(name, throwOnMissingValue: false);
    }

    private static RegistryKey BaseKey(RegistryHiveSelector hive)
        => hive == RegistryHiveSelector.CurrentUser ? Registry.CurrentUser : Registry.LocalMachine;
}
