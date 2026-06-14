using System.IO;
using System.Text;
using SteamAccountManager.Core.System;
using Xunit;

namespace SteamAccountManager.Core.Tests.System;

public class AtomicFileTests
{
    [Fact]
    public void Write_CreatesFileWithContent()
    {
        using var tmp = new TestPaths();
        var path = tmp.File("sub/data.txt");
        var sut = new AtomicFile();

        sut.Write(path, stream =>
        {
            var bytes = Encoding.UTF8.GetBytes("hello");
            stream.Write(bytes, 0, bytes.Length);
        });

        Assert.Equal("hello", File.ReadAllText(path));
    }

    [Fact]
    public void Write_OverwritesExistingFile_AndLeavesNoTempFiles()
    {
        using var tmp = new TestPaths();
        var path = tmp.File("data.txt");
        File.WriteAllText(path, "old-and-longer-content");
        var sut = new AtomicFile();

        sut.Write(path, stream =>
        {
            var bytes = Encoding.UTF8.GetBytes("new");
            stream.Write(bytes, 0, bytes.Length);
        });

        Assert.Equal("new", File.ReadAllText(path));
        Assert.Empty(Directory.GetFiles(tmp.Root, "*.tmp"));
    }
}
