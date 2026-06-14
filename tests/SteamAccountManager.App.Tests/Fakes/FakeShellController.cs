using SteamAccountManager.App.Services;

namespace SteamAccountManager.App.Tests.Fakes;

public sealed class FakeShellController : IShellController
{
    public int ShowCalls { get; private set; }
    public int ExitCalls { get; private set; }
    public int RefreshCalls { get; private set; }

    public void ShowMainWindow() => ShowCalls++;
    public void ExitApplication() => ExitCalls++;
    public void RefreshDashboard() => RefreshCalls++;
}
