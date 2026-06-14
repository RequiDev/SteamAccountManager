using System;
using System.IO;

namespace SteamAccountManager.App.Tests;

/// <summary>Unique temp directory for a test; deleted on Dispose. `using var tmp = new TestPaths();`</summary>
public sealed class TestPaths : IDisposable
{
    public string Root { get; }

    public TestPaths()
    {
        Root = Path.Combine(Path.GetTempPath(), "sam-app-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
    }

    public string File(string name) => Path.Combine(Root, name);

    public void Dispose()
    {
        try { Directory.Delete(Root, recursive: true); } catch { /* best effort */ }
    }
}
