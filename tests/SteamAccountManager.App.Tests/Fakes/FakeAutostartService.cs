using SteamAccountManager.Core.System;

namespace SteamAccountManager.App.Tests.Fakes;

public sealed class FakeAutostartService : IAutostartService
{
    private bool _enabled;
    public string? EnabledWithPath { get; private set; }
    public int EnableCallCount { get; private set; }
    public int DisableCallCount { get; private set; }

    public bool IsEnabled() => _enabled;
    public void Enable(string executablePath) { _enabled = true; EnabledWithPath = executablePath; EnableCallCount++; }
    public void Disable() { _enabled = false; EnabledWithPath = null; DisableCallCount++; }
}
