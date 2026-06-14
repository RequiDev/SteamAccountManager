using System;
using System.IO;

namespace SteamAccountManager.App.Infrastructure;

/// <summary>Resolves the app's on-disk data locations under %AppData%\SteamAccountManager.</summary>
public interface IAppPaths
{
    string BaseDirectory { get; }
    string MetadataFile { get; }
    string GroupsFile { get; }
    string SettingsFile { get; }
    string AvatarCacheDirectory { get; }
    string BackupsDirectory { get; }

    /// <summary>Creates the base directory and the avatar/backup sub-directories if missing.</summary>
    void EnsureCreated();
}

public sealed class AppPaths : IAppPaths
{
    public AppPaths(string? baseDirectory = null)
    {
        BaseDirectory = baseDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SteamAccountManager");
    }

    public string BaseDirectory { get; }
    public string MetadataFile => Path.Combine(BaseDirectory, "metadata.json");
    public string GroupsFile => Path.Combine(BaseDirectory, "groups.json");
    public string SettingsFile => Path.Combine(BaseDirectory, "settings.json");
    public string AvatarCacheDirectory => Path.Combine(BaseDirectory, "avatars");
    public string BackupsDirectory => Path.Combine(BaseDirectory, "backups");

    public void EnsureCreated()
    {
        Directory.CreateDirectory(BaseDirectory);
        Directory.CreateDirectory(AvatarCacheDirectory);
        Directory.CreateDirectory(BackupsDirectory);
    }
}
