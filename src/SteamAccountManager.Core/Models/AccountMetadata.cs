using System.Collections.Generic;

namespace SteamAccountManager.Core.Models;

/// <summary>App-owned metadata for an account, keyed by SteamId64. Persisted as JSON.</summary>
public sealed class AccountMetadata
{
    public string SteamId64 { get; set; } = "";
    public string? CustomLabel { get; set; }
    public string? Notes { get; set; }
    public List<string> GroupIds { get; set; } = new();
}
