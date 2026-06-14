using System;
using System.Collections.Generic;
using System.IO;
using SteamAccountManager.Core.Models;
using SteamAccountManager.Core.System;
using ValveKeyValue;

namespace SteamAccountManager.Core.Steam;

public interface ILoginUsersStore
{
    IReadOnlyList<SteamAccount> Read(string loginUsersPath);
    void SetActiveAccount(string loginUsersPath, string steamId64);
}

public sealed class LoginUsersStore : ILoginUsersStore
{
    private readonly IAtomicFile _atomicFile;

    public LoginUsersStore(IAtomicFile atomicFile) => _atomicFile = atomicFile;

    private static readonly KVSerializer Serializer =
        KVSerializer.Create(KVSerializationFormat.KeyValues1Text);

    public IReadOnlyList<SteamAccount> Read(string loginUsersPath)
    {
        if (!File.Exists(loginUsersPath))
        {
            return Array.Empty<SteamAccount>();
        }

        KVDocument doc;
        using (var fs = File.OpenRead(loginUsersPath))
        {
            doc = Serializer.Deserialize(fs);
        }

        var accounts = new List<SteamAccount>();
        foreach (KeyValuePair<string, KVObject> entry in doc.Root)
        {
            var acc = entry.Value;
            long.TryParse(VdfKeyValues.GetStringCI(acc, "Timestamp"), out var timestamp);

            accounts.Add(new SteamAccount
            {
                SteamId64 = entry.Key,
                AccountName = VdfKeyValues.GetStringCI(acc, "AccountName") ?? "",
                PersonaName = VdfKeyValues.GetStringCI(acc, "PersonaName") ?? "",
                MostRecent = VdfKeyValues.GetBoolCI(acc, "MostRecent"),
                RememberPassword = VdfKeyValues.GetBoolCI(acc, "RememberPassword"),
                AllowAutoLogin = VdfKeyValues.GetBoolCI(acc, "AllowAutoLogin"),
                Timestamp = timestamp,
            });
        }

        return accounts;
    }

    // SetActiveAccount is implemented in Task 5.
    public void SetActiveAccount(string loginUsersPath, string steamId64)
        => throw new NotImplementedException();
}
