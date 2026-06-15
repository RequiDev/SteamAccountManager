using System;
using System.Collections.Generic;
using System.Globalization;
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

    public void SetActiveAccount(string loginUsersPath, string steamId64)
    {
        KVDocument doc;
        using (var fs = File.OpenRead(loginUsersPath))
        {
            doc = Serializer.Deserialize(fs);
        }

        // Steam's login UI auto-selects the account with the HIGHEST Timestamp in
        // loginusers.vdf; the MostRecent flag alone is not sufficient. Find the current
        // maximum so we can make the target strictly newer and have Steam silently log
        // into it instead of falling back to the account picker.
        var found = false;
        long maxTimestamp = 0;
        foreach (KeyValuePair<string, KVObject> entry in doc.Root)
        {
            if (string.Equals(entry.Key, steamId64, StringComparison.Ordinal))
            {
                found = true;
            }

            if (long.TryParse(VdfKeyValues.GetStringCI(entry.Value, "Timestamp"), out var ts) && ts > maxTimestamp)
            {
                maxTimestamp = ts;
            }
        }

        if (!found)
        {
            throw new AccountNotFoundException(steamId64);
        }

        // Strictly newer than every other account, and never behind the wall clock.
        var newestTimestamp = Math.Max(maxTimestamp + 1, DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        foreach (KeyValuePair<string, KVObject> entry in doc.Root)
        {
            var isTarget = string.Equals(entry.Key, steamId64, StringComparison.Ordinal);

            // Exactly one account may be the auto-login account: the target opts in, all
            // others opt out (mirrors what Steam's own account switch writes).
            VdfKeyValues.SetStringPreservingCase(entry.Value, "MostRecent", isTarget ? "1" : "0");
            VdfKeyValues.SetStringPreservingCase(entry.Value, "AllowAutoLogin", isTarget ? "1" : "0");
            if (isTarget)
            {
                VdfKeyValues.SetStringPreservingCase(entry.Value, "RememberPassword", "1");
                VdfKeyValues.SetStringPreservingCase(
                    entry.Value, "Timestamp", newestTimestamp.ToString(CultureInfo.InvariantCulture));
            }
        }

        _atomicFile.Write(loginUsersPath, stream => Serializer.Serialize(stream, doc));

        // Validate that what we wrote re-parses; throws if we produced something invalid.
        using var verify = File.OpenRead(loginUsersPath);
        _ = Serializer.Deserialize(verify);
    }
}
