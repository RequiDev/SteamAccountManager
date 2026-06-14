using System;
using System.Collections.Generic;
using System.Linq;
using SteamAccountManager.Core.Models;

namespace SteamAccountManager.App.Models;

/// <summary>A UI-facing projection that joins a <see cref="SteamAccount"/> with its app metadata.</summary>
public sealed record AccountListItem(
    string SteamId64,
    string AccountName,
    string PersonaName,
    string? CustomLabel,
    IReadOnlyList<string> GroupIds,
    bool IsActive,
    DateTimeOffset? LastLogin)
{
    /// <summary>Free-form per-account notes from metadata. Init-only so positional fixtures stay unchanged.</summary>
    public string? Notes { get; init; }

    /// <summary>Whether a Steam token is cached for this account (it can auto-login without a sign-in).</summary>
    public bool IsTokenCached { get; init; }

    /// <summary>Label shown in the UI: custom label, else non-empty persona, else account name.</summary>
    public string DisplayName =>
        !string.IsNullOrWhiteSpace(CustomLabel) ? CustomLabel!
        : !string.IsNullOrWhiteSpace(PersonaName) ? PersonaName
        : AccountName;

    public static AccountListItem From(SteamAccount account, AccountMetadata? metadata)
        => new(
            SteamId64: account.SteamId64,
            AccountName: account.AccountName,
            PersonaName: account.PersonaName,
            CustomLabel: metadata?.CustomLabel,
            GroupIds: metadata?.GroupIds?.ToList() ?? new List<string>(),
            IsActive: account.MostRecent,
            LastLogin: account.Timestamp > 0
                ? DateTimeOffset.FromUnixTimeSeconds(account.Timestamp)
                : null)
        {
            Notes = metadata?.Notes,
        };
}
