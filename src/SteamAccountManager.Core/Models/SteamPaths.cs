namespace SteamAccountManager.Core.Models;

/// <summary>Resolved on-disk locations for a Steam installation.</summary>
public sealed record SteamPaths(
    string InstallDirectory,
    string ExecutablePath,
    string ConfigDirectory,
    string LoginUsersPath);
