namespace SteamAccountManager.Core.Models;

/// <summary>An account as read from Steam's loginusers.vdf.</summary>
public sealed class SteamAccount
{
    public required string SteamId64 { get; init; }
    public string AccountName { get; init; } = "";
    public string PersonaName { get; init; } = "";
    public bool MostRecent { get; init; }
    public bool RememberPassword { get; init; }
    public bool AllowAutoLogin { get; init; }
    public long Timestamp { get; init; }
}
