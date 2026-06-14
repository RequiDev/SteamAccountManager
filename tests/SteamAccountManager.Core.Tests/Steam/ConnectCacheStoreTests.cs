using System.Collections.Generic;
using System.IO;
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
        var vdf = tmp.WriteFile("a/local.vdf", SampleVdf(("714a299f1", "01000000AAAA"), ("2d0baba01", "01000000BBBB")));

        store.Capture(vdf);

        Assert.True(store.HasToken("bieelik1991")); // 714a299f1
        Assert.True(store.HasToken("dasrequi"));     // 2d0baba01
        Assert.False(store.HasToken("requi_cs2"));   // not captured
    }

    [Fact]
    public void Merge_InjectsTheFullUnion_AcrossDifferentLocalVdfs()
    {
        using var tmp = new TestPaths();
        var store = new ConnectCacheStore(tmp.File("connectcache.json"), new AtomicFile());

        // First Steam state had two accounts cached.
        var vdfA = tmp.WriteFile("a/local.vdf", SampleVdf(("714a299f1", "01000000AAAA"), ("2d0baba01", "01000000BBBB")));
        store.Capture(vdfA);

        // A later local.vdf only has one (different) account — Steam pruned the others.
        var vdfB = tmp.WriteFile("b/local.vdf", SampleVdf(("117d225c1", "01000000CCCC")));
        store.Merge(vdfB);

        // All three are now present in local.vdf, so any of them can silently auto-login.
        var keys = ReadKeys(vdfB);
        Assert.Equal(3, keys.Count);
        Assert.Contains("714a299f1", keys);
        Assert.Contains("2d0baba01", keys);
        Assert.Contains("117d225c1", keys);
    }

    [Fact]
    public void Capture_KeepsFreshestBlob_WhenAccountTokenChanges()
    {
        using var tmp = new TestPaths();
        var store = new ConnectCacheStore(tmp.File("connectcache.json"), new AtomicFile());

        store.Capture(tmp.WriteFile("v1/local.vdf", SampleVdf(("714a299f1", "01000000OLD"))));
        store.Capture(tmp.WriteFile("v2/local.vdf", SampleVdf(("714a299f1", "01000000NEW"))));

        var outVdf = tmp.WriteFile("out/local.vdf", SampleVdf(("zzz1", "01000000ZZ")));
        store.Merge(outVdf);

        var ser = KVSerializer.Create(KVSerializationFormat.KeyValues1Text);
        using var fs = File.OpenRead(outVdf);
        var cc = ser.Deserialize(fs).Root["Software"]["Valve"]["Steam"]["ConnectCache"];
        Assert.Equal("01000000NEW", cc["714a299f1"].ToString()); // refreshed blob wins
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
            "\t\t\t\t\"ConnectCache\"\n\t\t\t\t{\n\t\t\t\t\t\"714a299f1\"\t\t\"01000000AAAA\"\n\t\t\t\t}\n" +
            "\t\t\t}\n\t\t}\n\t}\n}\n";
        var path = tmp.WriteFile("local.vdf", vdf);

        store.Merge(path);

        var ser = KVSerializer.Create(KVSerializationFormat.KeyValues1Text);
        using var fs = File.OpenRead(path);
        var steam = ser.Deserialize(fs).Root["Software"]["Valve"]["Steam"];
        Assert.Equal("keepme", steam["SomethingElse"].ToString()); // sibling preserved, not clobbered
        Assert.Contains("714a299f1", ReadKeys(path));
    }

    [Fact]
    public void Persists_AcrossInstances()
    {
        using var tmp = new TestPaths();
        var persist = tmp.File("connectcache.json");
        var atomic = new AtomicFile();

        new ConnectCacheStore(persist, atomic)
            .Capture(tmp.WriteFile("a/local.vdf", SampleVdf(("714a299f1", "01000000AAAA"))));

        var reloaded = new ConnectCacheStore(persist, atomic);
        Assert.True(reloaded.HasToken("bieelik1991"));
    }
}
