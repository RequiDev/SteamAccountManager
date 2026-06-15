namespace SteamAccountManager.Core.Steam;

/// <summary>How usable an account's cached login token is for silent auto-login.</summary>
public enum TokenStatus
{
    /// <summary>No cached token exists for this account.</summary>
    Missing,

    /// <summary>A token exists but can't be decrypted on this Windows user/machine
    /// (it was copied from another PC/account, or is corrupt) — it can't auto-login here.</summary>
    ForeignMachine,

    /// <summary>A valid token exists but has expired; one fresh sign-in re-mints it.</summary>
    Expired,

    /// <summary>A valid, unexpired token is cached — the account can auto-login silently.</summary>
    Ready,
}
