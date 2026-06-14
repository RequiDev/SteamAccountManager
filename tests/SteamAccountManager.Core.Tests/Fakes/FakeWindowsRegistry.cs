using System.Collections.Generic;
using SteamAccountManager.Core.System;

namespace SteamAccountManager.Core.Tests.Fakes;

/// <summary>In-memory IWindowsRegistry for tests.</summary>
public sealed class FakeWindowsRegistry : IWindowsRegistry
{
    private readonly Dictionary<string, object> _values = new();

    private static string Key(RegistryHiveSelector hive, string subKey, string name)
        => $"{hive}|{subKey}|{name}";

    public string? GetString(RegistryHiveSelector hive, string subKey, string name)
        => _values.TryGetValue(Key(hive, subKey, name), out var v) ? v as string : null;

    public int? GetDword(RegistryHiveSelector hive, string subKey, string name)
        => _values.TryGetValue(Key(hive, subKey, name), out var v) && v is int i ? i : null;

    public void SetString(RegistryHiveSelector hive, string subKey, string name, string value)
        => _values[Key(hive, subKey, name)] = value;

    public void SetDword(RegistryHiveSelector hive, string subKey, string name, int value)
        => _values[Key(hive, subKey, name)] = value;

    public void DeleteValue(RegistryHiveSelector hive, string subKey, string name)
        => _values.Remove(Key(hive, subKey, name));
}
