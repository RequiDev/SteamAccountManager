using SteamAccountManager.Core.Steam;
using SteamAccountManager.Core.System;
using Xunit;

namespace SteamAccountManager.Core.Tests.System;

public class Crc32Tests
{
    [Fact]
    public void HashHex_MatchesStandardCrc32Vector()
    {
        Assert.Equal("cbf43926", Crc32.HashHex("123456789")); // canonical CRC-32 check value
    }

    [Theory]
    [InlineData("bieelik1991", "714a299f1")]
    [InlineData("dasrequi", "2d0baba01")]
    [InlineData("bl4zing_ic3_m4n@hotmail.com", "117d225c1")]
    public void KeyFor_MatchesSteamConnectCacheKeys(string accountName, string expectedKey)
    {
        // Verified against the real local.vdf on a live install.
        Assert.Equal(expectedKey, ConnectCacheStore.KeyFor(accountName));
    }

    [Fact]
    public void KeyFor_IsCaseInsensitive()
    {
        Assert.Equal(ConnectCacheStore.KeyFor("bieelik1991"), ConnectCacheStore.KeyFor("BIEELIK1991"));
    }

    [Fact]
    public void KeyFor_DoesNotZeroPad_MatchingSteamPrintfX()
    {
        // Steam keys ConnectCache with printf "%x%x" (no zero-padding); "user12"'s CRC is 0x01584af3,
        // so the key must be "1584af31" (7-digit hash + "1"), NOT a zero-padded "01584af31".
        Assert.Equal("1584af31", ConnectCacheStore.KeyFor("user12"));
    }
}
