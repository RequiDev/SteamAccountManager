using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using SteamAccountManager.Core.Steam;
using SteamAccountManager.Core.System;
using ValveKeyValue;
using Xunit;

namespace SteamAccountManager.Core.Tests.Steam;

public class ConnectCacheStoreTests
{
    private static string SampleVdf(params (string key, string val)[] entries)
    {
        var sb = new StringBuilder();
        sb.Append("\"MachineUserConfigStore\"\n{\n\t\"Software\"\n\t{\n\t\t\"Valve\"\n\t\t{\n\t\t\t\"Steam\"\n\t\t\t{\n\t\t\t\t\"ConnectCache\"\n\t\t\t\t{\n");
        foreach (var (k, v) in entries)
        {
            sb.Append($"\t\t\t\t\t\"{k}\"\t\t\"{v}\"\n");
        }

        sb.Append("\t\t\t\t}\n\t\t\t}\n\t\t}\n\t}\n}\n");
        return sb.ToString();
    }

    private static List<string> ReadKeys(string vdfPath)
    {
        var ser = KVSerializer.Create(KVSerializationFormat.KeyValues1Text);
        using var fs = File.OpenRead(vdfPath);
        var doc = ser.Deserialize(fs);
        var cc = doc.Root["Software"]["Valve"]["Steam"]["ConnectCache"];
        var keys = new List<string>();
        foreach (var kv in cc)
        {
            keys.Add(kv.Key);
        }

        return keys;
    }

    [Fact]
    public void Capture_RecordsEntries_AndHasTokenReflectsThem()
    {
        using var tmp = new TestPaths();
        var store = new ConnectCacheStore(tmp.File("connectcache.json"), new AtomicFile());
        var vdf = tmp.WriteFile("a/local.vdf", SampleVdf(("15189d6c1", "01000000AAAA"), ("cc631c8f1", "01000000BBBB")));

        store.Capture(vdf);

        Assert.True(store.HasToken("acct_alpha")); // 15189d6c1
        Assert.True(store.HasToken("acct_bravo"));     // cc631c8f1
        Assert.False(store.HasToken("acct_delta"));   // not captured
    }

    [Fact]
    public void Merge_InjectsTheFullUnion_AcrossDifferentLocalVdfs()
    {
        using var tmp = new TestPaths();
        var store = new ConnectCacheStore(tmp.File("connectcache.json"), new AtomicFile());

        // First Steam state had two accounts cached.
        var vdfA = tmp.WriteFile("a/local.vdf", SampleVdf(("15189d6c1", "01000000AAAA"), ("cc631c8f1", "01000000BBBB")));
        store.Capture(vdfA);

        // A later local.vdf only has one (different) account — Steam pruned the others.
        var vdfB = tmp.WriteFile("b/local.vdf", SampleVdf(("2c5f530d1", "01000000CCCC")));
        store.Merge(vdfB);

        // All three are now present in local.vdf, so any of them can silently auto-login.
        var keys = ReadKeys(vdfB);
        Assert.Equal(3, keys.Count);
        Assert.Contains("15189d6c1", keys);
        Assert.Contains("cc631c8f1", keys);
        Assert.Contains("2c5f530d1", keys);
    }

    [Fact]
    public void Capture_KeepsFreshestBlob_WhenAccountTokenChanges()
    {
        using var tmp = new TestPaths();
        var store = new ConnectCacheStore(tmp.File("connectcache.json"), new AtomicFile());

        store.Capture(tmp.WriteFile("v1/local.vdf", SampleVdf(("15189d6c1", "01000000OLD"))));
        store.Capture(tmp.WriteFile("v2/local.vdf", SampleVdf(("15189d6c1", "01000000NEW"))));

        var outVdf = tmp.WriteFile("out/local.vdf", SampleVdf(("zzz1", "01000000ZZ")));
        store.Merge(outVdf);

        var ser = KVSerializer.Create(KVSerializationFormat.KeyValues1Text);
        using var fs = File.OpenRead(outVdf);
        var cc = ser.Deserialize(fs).Root["Software"]["Valve"]["Steam"]["ConnectCache"];
        Assert.Equal("01000000NEW", cc["15189d6c1"].ToString()); // refreshed blob wins
    }

    [Fact]
    public void Merge_PreservesSiblingContentUnderSteam()
    {
        using var tmp = new TestPaths();
        var store = new ConnectCacheStore(tmp.File("connectcache.json"), new AtomicFile());
        // A local.vdf where the Steam node has a sibling next to ConnectCache.
        var vdf =
            "\"MachineUserConfigStore\"\n{\n\t\"Software\"\n\t{\n\t\t\"Valve\"\n\t\t{\n\t\t\t\"Steam\"\n\t\t\t{\n" +
            "\t\t\t\t\"SomethingElse\"\t\t\"keepme\"\n" +
            "\t\t\t\t\"ConnectCache\"\n\t\t\t\t{\n\t\t\t\t\t\"15189d6c1\"\t\t\"01000000AAAA\"\n\t\t\t\t}\n" +
            "\t\t\t}\n\t\t}\n\t}\n}\n";
        var path = tmp.WriteFile("local.vdf", vdf);

        store.Merge(path);

        var ser = KVSerializer.Create(KVSerializationFormat.KeyValues1Text);
        using var fs = File.OpenRead(path);
        var steam = ser.Deserialize(fs).Root["Software"]["Valve"]["Steam"];
        Assert.Equal("keepme", steam["SomethingElse"].ToString()); // sibling preserved, not clobbered
        Assert.Contains("15189d6c1", ReadKeys(path));
    }

    [Fact]
    public void Persists_AcrossInstances()
    {
        using var tmp = new TestPaths();
        var persist = tmp.File("connectcache.json");
        var atomic = new AtomicFile();

        new ConnectCacheStore(persist, atomic)
            .Capture(tmp.WriteFile("a/local.vdf", SampleVdf(("15189d6c1", "01000000AAAA"))));

        var reloaded = new ConnectCacheStore(persist, atomic);
        Assert.True(reloaded.HasToken("acct_alpha"));
    }

    [Fact]
    public void GetStatus_Missing_WhenNoEntry()
    {
        using var tmp = new TestPaths();
        var store = new ConnectCacheStore(tmp.File("cc.json"), new AtomicFile());
        store.Capture(tmp.WriteFile("a/local.vdf", SampleVdf(("15189d6c1", "01000000AAAA"))));

        Assert.Equal(TokenStatus.Missing, store.GetStatus("acct_delta")); // not captured
    }

    [Fact]
    public void GetStatus_Ready_WhenUnexpiredTokenForThisMachine()
    {
        using var tmp = new TestPaths();
        var store = new ConnectCacheStore(tmp.File("cc.json"), new AtomicFile());
        var future = DateTimeOffset.UtcNow.AddDays(30).ToUnixTimeSeconds();
        var key = ConnectCacheStore.KeyFor("acct_bravo");
        store.Capture(tmp.WriteFile("a/local.vdf", SampleVdf((key, ProtectHex(MakeJwt(future), "acct_bravo")))));

        Assert.Equal(TokenStatus.Ready, store.GetStatus("acct_bravo"));
    }

    [Fact]
    public void GetStatus_Expired_WhenTokenPastExpiry()
    {
        using var tmp = new TestPaths();
        var store = new ConnectCacheStore(tmp.File("cc.json"), new AtomicFile());
        var past = DateTimeOffset.UtcNow.AddDays(-1).ToUnixTimeSeconds();
        var key = ConnectCacheStore.KeyFor("acct_bravo");
        store.Capture(tmp.WriteFile("a/local.vdf", SampleVdf((key, ProtectHex(MakeJwt(past), "acct_bravo")))));

        Assert.Equal(TokenStatus.Expired, store.GetStatus("acct_bravo"));
    }

    [Fact]
    public void GetStatus_ForeignMachine_WhenTokenEntropyDoesNotMatch()
    {
        using var tmp = new TestPaths();
        var store = new ConnectCacheStore(tmp.File("cc.json"), new AtomicFile());
        var future = DateTimeOffset.UtcNow.AddDays(30).ToUnixTimeSeconds();
        var key = ConnectCacheStore.KeyFor("acct_bravo");
        // Encrypted with a different account's entropy: it won't decrypt as "acct_bravo" here.
        store.Capture(tmp.WriteFile("a/local.vdf", SampleVdf((key, ProtectHex(MakeJwt(future), "someone-else")))));

        Assert.Equal(TokenStatus.ForeignMachine, store.GetStatus("acct_bravo"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void IsUnexpiredJwt_RespectsExpiry(bool inFuture)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var jwt = MakeJwt(inFuture ? now + 3600 : now - 3600);

        Assert.Equal(inFuture, ConnectCacheStore.IsUnexpiredJwt(jwt, now));
    }

    [Theory]
    [InlineData("not.a.jwt")]
    [InlineData("garbage")]
    [InlineData("")]
    public void IsUnexpiredJwt_FalseOnNonJwt(string jwt)
    {
        Assert.False(ConnectCacheStore.IsUnexpiredJwt(jwt, DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
    }

    [Fact]
    public void Capture_ReadsConnectCache_WhenPathCasingDiffers()
    {
        using var tmp = new TestPaths();
        var store = new ConnectCacheStore(tmp.File("cc.json"), new AtomicFile());
        var path = tmp.WriteFile("local.vdf", MixedCaseVdf("15189d6c1", "01000000AAAA"));

        store.Capture(path);

        Assert.True(store.HasToken("acct_alpha")); // 15189d6c1 — reachable despite a lowercase "valve" node
    }

    [Fact]
    public void Merge_ReusesExistingPathNode_WhenCasingDiffers_WithoutDuplicatingBranch()
    {
        using var tmp = new TestPaths();
        var store = new ConnectCacheStore(tmp.File("cc.json"), new AtomicFile());
        var path = tmp.WriteFile("local.vdf", MixedCaseVdf("15189d6c1", "01000000AAAA"));

        store.Merge(path);

        var text = File.ReadAllText(path);
        Assert.Contains("\"valve\"", text);        // original lowercase casing preserved
        Assert.DoesNotContain("\"Valve\"", text);  // no duplicate capital-cased branch created
        Assert.Contains("15189d6c1", text);
    }

    // Real-world: Steam writes this path's casing inconsistently across installs ("valve" lowercase).
    private static string MixedCaseVdf(string key, string val) =>
        "\"MachineUserConfigStore\"\n{\n\t\"Software\"\n\t{\n\t\t\"valve\"\n\t\t{\n\t\t\t\"Steam\"\n\t\t\t{\n" +
        "\t\t\t\t\"ConnectCache\"\n\t\t\t\t{\n" +
        $"\t\t\t\t\t\"{key}\"\t\t\"{val}\"\n" +
        "\t\t\t\t}\n\t\t\t}\n\t\t}\n\t}\n}\n";

    private static string MakeJwt(long exp)
    {
        static string B64Url(string json) =>
            Convert.ToBase64String(Encoding.UTF8.GetBytes(json)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        var header = B64Url("{ \"typ\": \"JWT\", \"alg\": \"EdDSA\" }");
        var payload = B64Url($"{{ \"exp\": {exp} }}");
        return header + "." + payload + ".sig";
    }

    private static string ProtectHex(string jwt, string entropyName)
    {
        var blob = ProtectedData.Protect(
            Encoding.ASCII.GetBytes(jwt),
            Encoding.UTF8.GetBytes(entropyName.ToLowerInvariant()),
            DataProtectionScope.CurrentUser);
        return Convert.ToHexString(blob);
    }
}
