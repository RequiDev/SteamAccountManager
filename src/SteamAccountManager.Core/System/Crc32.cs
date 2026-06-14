using System;
using System.Text;

namespace SteamAccountManager.Core.System;

/// <summary>
/// Standard IEEE CRC-32 (polynomial 0xEDB88320). This is the hash Steam's client uses to key
/// each account's entry in the local.vdf ConnectCache: key = hex(CRC32(lowercase(accountName))) + "1".
/// </summary>
public static class Crc32
{
    private static readonly uint[] Table = BuildTable();

    public static uint Compute(ReadOnlySpan<byte> data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in data)
        {
            crc = (crc >> 8) ^ Table[(crc ^ b) & 0xFF];
        }

        return crc ^ 0xFFFFFFFFu;
    }

    /// <summary>CRC-32 of the ASCII bytes of <paramref name="text"/>, as lowercase hex with no leading zeros (printf %x).</summary>
    public static string HashHex(string text) => Compute(Encoding.ASCII.GetBytes(text)).ToString("x");

    private static uint[] BuildTable()
    {
        var table = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            var c = i;
            for (var k = 0; k < 8; k++)
            {
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            }

            table[i] = c;
        }

        return table;
    }
}
