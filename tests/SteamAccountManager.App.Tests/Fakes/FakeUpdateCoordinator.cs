using System.Collections.Generic;
using System.Threading.Tasks;
using SteamAccountManager.App.Services;

namespace SteamAccountManager.App.Tests.Fakes;

public sealed class FakeUpdateCoordinator : IUpdateCoordinator
{
    public List<bool> Checks { get; } = new();

    public Task CheckAsync(bool userInitiated)
    {
        Checks.Add(userInitiated);
        return Task.CompletedTask;
    }
}
