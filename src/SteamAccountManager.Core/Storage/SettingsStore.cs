using SteamAccountManager.Core.Models;
using SteamAccountManager.Core.System;

namespace SteamAccountManager.Core.Storage;

public interface ISettingsStore
{
    AppSettings Load();
    void Save(AppSettings settings);
}

public sealed class SettingsStore : ISettingsStore
{
    private readonly string _path;
    private readonly IAtomicFile _atomicFile;

    public SettingsStore(string path, IAtomicFile atomicFile)
    {
        _path = path;
        _atomicFile = atomicFile;
    }

    public AppSettings Load() => JsonFile.Load(_path, () => new AppSettings());

    public void Save(AppSettings settings) => JsonFile.Save(_path, settings, _atomicFile);
}
