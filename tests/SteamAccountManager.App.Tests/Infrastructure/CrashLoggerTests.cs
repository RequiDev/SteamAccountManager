using System;
using System.IO;
using System.Linq;
using SteamAccountManager.App.Infrastructure;
using Xunit;

namespace SteamAccountManager.App.Tests.Infrastructure;

public class CrashLoggerTests
{
    [Fact]
    public void Log_WritesFileWithSourceAndExceptionDetails()
    {
        using var tmp = new TestPaths();

        var path = CrashLogger.Log(new InvalidOperationException("boom"), "DispatcherUnhandledException", tmp.Root);

        Assert.NotNull(path);
        Assert.True(File.Exists(path));
        var content = File.ReadAllText(path!);
        Assert.Contains("DispatcherUnhandledException", content);
        Assert.Contains("InvalidOperationException", content);
        Assert.Contains("boom", content);
    }

    [Fact]
    public void Log_CreatesDirectory_AndDoesNotThrowOnRepeatedCalls()
    {
        using var tmp = new TestPaths();
        var dir = tmp.File("nested/logs");

        var first = CrashLogger.Log(new Exception("a"), "src", dir);
        var second = CrashLogger.Log(new Exception("b"), "src", dir);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotEqual(first, second); // distinct timestamped files
        Assert.Equal(2, Directory.GetFiles(dir).Count(f => f.EndsWith(".log")));
    }
}
