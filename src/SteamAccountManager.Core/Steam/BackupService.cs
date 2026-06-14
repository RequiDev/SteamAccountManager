using System.IO;

namespace SteamAccountManager.Core.Steam;

public interface IBackupService
{
    void Backup(string filePath);
    void Restore(string filePath);
    bool HasBackup(string filePath);
}

/// <summary>Keeps a single rolling ".last.bak" copy per file, restored on switch failure.</summary>
public sealed class BackupService : IBackupService
{
    private readonly string _backupDirectory;

    public BackupService(string backupDirectory) => _backupDirectory = backupDirectory;

    private string BackupPathFor(string filePath)
        => Path.Combine(_backupDirectory, Path.GetFileName(filePath) + ".last.bak");

    public void Backup(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return;
        }

        Directory.CreateDirectory(_backupDirectory);
        File.Copy(filePath, BackupPathFor(filePath), overwrite: true);
    }

    public bool HasBackup(string filePath) => File.Exists(BackupPathFor(filePath));

    public void Restore(string filePath)
    {
        var backup = BackupPathFor(filePath);
        if (File.Exists(backup))
        {
            File.Copy(backup, filePath, overwrite: true);
        }
    }
}
