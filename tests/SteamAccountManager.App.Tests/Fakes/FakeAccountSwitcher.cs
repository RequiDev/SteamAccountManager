using System.Collections.Generic;
using SteamAccountManager.Core.Steam;

namespace SteamAccountManager.App.Tests.Fakes;

public sealed class FakeAccountSwitcher : IAccountSwitcher
{
    public List<string> SwitchedTo { get; } = new();
    public int BeginAddCalls { get; private set; }
    public int CaptureCalls { get; private set; }

    public void SwitchTo(string steamId64) => SwitchedTo.Add(steamId64);
    public void BeginAddAccount() => BeginAddCalls++;
    public void CaptureTokens() => CaptureCalls++;
}
