using System.IO;
using SteamAccountManager.Core.Steam;
using Xunit;

namespace SteamAccountManager.Core.Tests.Steam;

public class BackupServiceTests
{
    [Fact]
    public void Backup_ThenRestore_RecoversOriginalContent()
    {
        using var tmp = new TestPaths();
        var file = tmp.WriteFile("config/loginusers.vdf", "ORIGINAL");
        var sut = new BackupService(tmp.File("backups"));

        sut.Backup(file);
        Assert.True(sut.HasBackup(file));

        File.WriteAllText(file, "CORRUPTED");
        sut.Restore(file);

        Assert.Equal("ORIGINAL", File.ReadAllText(file));
    }

    [Fact]
    public void Backup_DoesNothing_WhenSourceMissing()
    {
        using var tmp = new TestPaths();
        var sut = new BackupService(tmp.File("backups"));

        sut.Backup(tmp.File("missing.vdf")); // must not throw

        Assert.False(sut.HasBackup(tmp.File("missing.vdf")));
    }

    [Fact]
    public void Restore_DoesNothing_WhenNoBackupExists()
    {
        using var tmp = new TestPaths();
        var file = tmp.WriteFile("loginusers.vdf", "LIVE");
        var sut = new BackupService(tmp.File("backups"));

        sut.Restore(file); // must not throw or change the file

        Assert.Equal("LIVE", File.ReadAllText(file));
    }
}
