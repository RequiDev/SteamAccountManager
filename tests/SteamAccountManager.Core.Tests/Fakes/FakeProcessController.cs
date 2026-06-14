using System;
using System.Collections.Generic;
using SteamAccountManager.Core.Steam;

namespace SteamAccountManager.Core.Tests.Fakes;

public sealed class FakeProcessController : ISteamProcessController
{
    public bool Running { get; set; }
    public bool ShutdownResult { get; set; } = true;

    public bool ShutdownCalled { get; private set; }
    public bool LaunchCalled { get; private set; }
    public string? LaunchArguments { get; private set; }

    /// <summary>Records the order of operations, e.g. ["shutdown", "launch"].</summary>
    public List<string> Calls { get; } = new();

    public bool IsSteamRunning() => Running;

    public bool ShutdownAndWait(TimeSpan timeout)
    {
        ShutdownCalled = true;
        Calls.Add("shutdown");
        if (ShutdownResult)
        {
            Running = false;
        }

        return ShutdownResult;
    }

    public void Launch(string? arguments = null)
    {
        LaunchCalled = true;
        LaunchArguments = arguments;
        Calls.Add("launch");
    }
}
