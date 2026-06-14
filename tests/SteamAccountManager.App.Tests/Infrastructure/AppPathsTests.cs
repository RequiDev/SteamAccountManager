using System;
using System.IO;
using SteamAccountManager.App.Infrastructure;
using Xunit;

namespace SteamAccountManager.App.Tests.Infrastructure;

public class AppPathsTests
{
    [Fact]
    public void DefaultBaseDirectory_IsUnderAppData()
    {
        var sut = new AppPaths();

        var expected = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SteamAccountManager");

        Assert.Equal(expected, sut.BaseDirectory);
    }

    [Fact]
    public void AllPaths_AreRootedUnderTheBaseDirectory()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), "sam-app-tests", Guid.NewGuid().ToString("N"));
        var sut = new AppPaths(baseDir);

        Assert.Equal(baseDir, sut.BaseDirectory);
        Assert.Equal(Path.Combine(baseDir, "metadata.json"), sut.MetadataFile);
        Assert.Equal(Path.Combine(baseDir, "groups.json"), sut.GroupsFile);
        Assert.Equal(Path.Combine(baseDir, "settings.json"), sut.SettingsFile);
        Assert.Equal(Path.Combine(baseDir, "avatars"), sut.AvatarCacheDirectory);
        Assert.Equal(Path.Combine(baseDir, "backups"), sut.BackupsDirectory);
        Assert.Equal(Path.Combine(baseDir, "tokens"), sut.TokensDirectory);
        Assert.Equal(Path.Combine(baseDir, "tokens", "connectcache.json"), sut.ConnectCacheFile);
    }

    [Fact]
    public void EnsureCreated_CreatesBaseAndSubDirectories()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), "sam-app-tests", Guid.NewGuid().ToString("N"));
        var sut = new AppPaths(baseDir);

        sut.EnsureCreated();

        Assert.True(Directory.Exists(sut.BaseDirectory));
        Assert.True(Directory.Exists(sut.AvatarCacheDirectory));
        Assert.True(Directory.Exists(sut.BackupsDirectory));
        Assert.True(Directory.Exists(sut.TokensDirectory));

        Directory.Delete(baseDir, recursive: true);
    }
}
