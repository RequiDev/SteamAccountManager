using System.Threading;
using System.Threading.Tasks;
using SteamAccountManager.Core.Avatars;

namespace SteamAccountManager.Core.Tests.Fakes;

public sealed class FakeAvatarFetcher : IAvatarFetcher
{
    private readonly byte[]? _bytes;
    public int CallCount { get; private set; }

    public FakeAvatarFetcher(byte[]? bytes) => _bytes = bytes;

    public Task<byte[]?> FetchAsync(string steamId64, CancellationToken ct = default)
    {
        CallCount++;
        return Task.FromResult(_bytes);
    }
}
