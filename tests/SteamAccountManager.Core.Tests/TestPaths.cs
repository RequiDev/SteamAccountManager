using System;
using System.IO;

namespace SteamAccountManager.Core.Tests;

/// <summary>
/// Creates a unique temp directory for a test and deletes it on Dispose.
/// Use with `using var tmp = new TestPaths();` inside a test.
/// </summary>
public sealed class TestPaths : IDisposable
{
    public string Root { get; }

    public TestPaths()
    {
        Root = Path.Combine(Path.GetTempPath(), "sam-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
    }

    public string File(string name) => Path.Combine(Root, name);

    public string WriteFile(string name, string content)
    {
        var path = File(name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        System.IO.File.WriteAllText(path, content);
        return path;
    }

    public void Dispose()
    {
        try { Directory.Delete(Root, recursive: true); } catch { /* best effort cleanup */ }
    }
}
