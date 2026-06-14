using SteamAccountManager.Core.Models;
using SteamAccountManager.Core.Steam;

namespace SteamAccountManager.App.Tests.Fakes;

public sealed class FakeSteamLocator : ISteamLocator
{
    private readonly SteamPaths? _paths;
    public FakeSteamLocator(SteamPaths? paths) => _paths = paths;
    public SteamPaths? Locate() => _paths;
}
