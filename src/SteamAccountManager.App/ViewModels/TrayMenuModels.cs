using System;
using System.Collections.Generic;

namespace SteamAccountManager.App.ViewModels;

/// <summary>One account leaf in the tray menu.</summary>
public sealed record TrayAccountItem(string SteamId64, string DisplayName, string AccountName, bool IsActive)
{
    /// <summary>
    /// Text shown in the tray menu: the display name with the (unique) Steam login name in
    /// parentheses, so accounts that share a display name stay distinguishable. The suffix is
    /// dropped when the display name already is the login name (avoids a redundant "name (name)").
    /// </summary>
    public string MenuLabel =>
        string.Equals(DisplayName, AccountName, StringComparison.Ordinal)
            ? DisplayName
            : $"{DisplayName} ({AccountName})";
}

/// <summary>One group (or the "Ungrouped" bucket) and its accounts in the tray menu.</summary>
public sealed record TrayGroupItem(string Header, IReadOnlyList<TrayAccountItem> Accounts);
