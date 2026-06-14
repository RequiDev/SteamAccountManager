using System.Collections.Generic;

namespace SteamAccountManager.App.ViewModels;

/// <summary>One account leaf in the tray menu.</summary>
public sealed record TrayAccountItem(string SteamId64, string DisplayName, bool IsActive);

/// <summary>One group (or the "Ungrouped" bucket) and its accounts in the tray menu.</summary>
public sealed record TrayGroupItem(string Header, IReadOnlyList<TrayAccountItem> Accounts);
