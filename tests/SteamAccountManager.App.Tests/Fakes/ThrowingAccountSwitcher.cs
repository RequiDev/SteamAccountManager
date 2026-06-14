using System;
using SteamAccountManager.Core.Steam;

namespace SteamAccountManager.App.Tests.Fakes;

public sealed class ThrowingAccountSwitcher : IAccountSwitcher
{
    public void SwitchTo(string steamId64) => throw new InvalidOperationException("boom");
    public void BeginAddAccount() => throw new InvalidOperationException("boom");
    public void CaptureTokens() { } // best-effort; never throws
}
