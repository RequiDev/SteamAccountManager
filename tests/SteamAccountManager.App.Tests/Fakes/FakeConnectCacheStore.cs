using System.Collections.Generic;
using SteamAccountManager.Core.Steam;

namespace SteamAccountManager.App.Tests.Fakes;

public sealed class FakeConnectCacheStore : IConnectCacheStore
{
    public HashSet<string> CachedAccounts { get; } = new();
    public int CaptureCalls { get; private set; }
    public int MergeCalls { get; private set; }

    public void Capture(string localVdfPath) => CaptureCalls++;
    public void Merge(string localVdfPath) => MergeCalls++;
    public bool HasToken(string accountName) => CachedAccounts.Contains(accountName);
}
