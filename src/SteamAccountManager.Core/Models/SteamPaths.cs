namespace SteamAccountManager.Core.Models;

/// <summary>Resolved on-disk locations for a Steam installation.</summary>
/// <param name="LocalVdfPath">
/// %LocalAppData%\Steam\local.vdf — the per-account refresh-token store (ConnectCache) that
/// actually gates silent auto-login. It lives under LocalAppData, not the install directory.
/// </param>
public sealed record SteamPaths(
    string InstallDirectory,
    string ExecutablePath,
    string ConfigDirectory,
    string LoginUsersPath,
    string LocalVdfPath);
