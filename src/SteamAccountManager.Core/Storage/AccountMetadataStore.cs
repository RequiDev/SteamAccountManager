using System.Collections.Generic;
using SteamAccountManager.Core.Models;
using SteamAccountManager.Core.System;

namespace SteamAccountManager.Core.Storage;

public interface IAccountMetadataStore
{
    AccountMetadata Get(string steamId64);
    void Upsert(AccountMetadata metadata);
    IReadOnlyDictionary<string, AccountMetadata> GetAll();
}

public sealed class AccountMetadataStore : IAccountMetadataStore
{
    private readonly string _path;
    private readonly IAtomicFile _atomicFile;
    private readonly Dictionary<string, AccountMetadata> _data;

    public AccountMetadataStore(string path, IAtomicFile atomicFile)
    {
        _path = path;
        _atomicFile = atomicFile;
        _data = JsonFile.Load(path, () => new Dictionary<string, AccountMetadata>());
    }

    public AccountMetadata Get(string steamId64)
        => _data.TryGetValue(steamId64, out var meta)
            ? meta
            : new AccountMetadata { SteamId64 = steamId64 };

    public void Upsert(AccountMetadata metadata)
    {
        _data[metadata.SteamId64] = metadata;
        JsonFile.Save(_path, _data, _atomicFile);
    }

    public IReadOnlyDictionary<string, AccountMetadata> GetAll() => _data;
}
