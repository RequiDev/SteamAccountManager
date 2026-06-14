using System.Threading;
using System.Threading.Tasks;
using SteamAccountManager.Core.Avatars;

namespace SteamAccountManager.App.Tests.Fakes;

public sealed class FakeAvatarService : IAvatarService
{
    public Task<string?> GetAvatarAsync(string steamId64, CancellationToken ct = default)
        => Task.FromResult<string?>($@"C:\cache\{steamId64}.jpg");
}
