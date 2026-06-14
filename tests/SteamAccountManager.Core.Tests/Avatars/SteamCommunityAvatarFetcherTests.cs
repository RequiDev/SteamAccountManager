using SteamAccountManager.Core.Avatars;
using Xunit;

namespace SteamAccountManager.Core.Tests.Avatars;

public class SteamCommunityAvatarFetcherTests
{
    [Fact]
    public void ParseAvatarUrl_ExtractsAvatarFull()
    {
        const string xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <profile>
              <steamID64>76561198000000001</steamID64>
              <avatarFull><![CDATA[https://avatars.steamstatic.com/abc_full.jpg]]></avatarFull>
            </profile>
            """;

        Assert.Equal(
            "https://avatars.steamstatic.com/abc_full.jpg",
            SteamCommunityAvatarFetcher.ParseAvatarUrl(xml));
    }

    [Fact]
    public void ParseAvatarUrl_ReturnsNull_WhenAbsent()
    {
        Assert.Null(SteamCommunityAvatarFetcher.ParseAvatarUrl("<profile></profile>"));
    }
}
