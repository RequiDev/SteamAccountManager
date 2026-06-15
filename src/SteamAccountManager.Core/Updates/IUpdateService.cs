using System;
using System.Threading;
using System.Threading.Tasks;

namespace SteamAccountManager.Core.Updates;

/// <summary>A newer release available for download.</summary>
public sealed record UpdateInfo(Version Version, string TagName, string ExeDownloadUrl, string ReleaseUrl);

public interface IUpdateService
{
    /// <summary>
    /// Returns the latest release if it is newer than <paramref name="currentVersion"/> and ships
    /// the expected executable asset; otherwise <c>null</c>. Throws on network/parse failure.
    /// </summary>
    Task<UpdateInfo?> CheckForUpdateAsync(Version currentVersion, CancellationToken ct = default);

    /// <summary>Downloads the update's executable to <paramref name="destinationPath"/>.</summary>
    Task DownloadAsync(UpdateInfo update, string destinationPath, CancellationToken ct = default);
}
