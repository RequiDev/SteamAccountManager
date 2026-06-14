using System;

namespace SteamAccountManager.Core.Models;

/// <summary>Thrown when no Steam installation can be located.</summary>
public sealed class SteamNotFoundException : Exception
{
    public SteamNotFoundException()
        : base("Could not locate a Steam installation on this machine.") { }
}

/// <summary>Thrown when a requested account is not present in loginusers.vdf.</summary>
public sealed class AccountNotFoundException : Exception
{
    public AccountNotFoundException(string steamId64)
        : base($"No account with SteamID '{steamId64}' was found.") => SteamId64 = steamId64;

    public string SteamId64 { get; }
}

/// <summary>Thrown when Steam could not be shut down within the allotted time.</summary>
public sealed class SteamShutdownException : Exception
{
    public SteamShutdownException()
        : base("Steam did not shut down within the timeout. Aborting to avoid clobbering its files.") { }
}
