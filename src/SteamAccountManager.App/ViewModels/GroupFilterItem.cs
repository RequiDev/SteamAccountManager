namespace SteamAccountManager.App.ViewModels;

/// <summary>An entry in the left filter pane: "All", a named group, or "Ungrouped".</summary>
public sealed record GroupFilterItem(string Label, string? GroupId, bool IsAll, bool IsUngrouped)
{
    public static GroupFilterItem All() => new("All accounts", null, IsAll: true, IsUngrouped: false);
    public static GroupFilterItem Ungrouped() => new("Ungrouped", null, IsAll: false, IsUngrouped: true);
    public static GroupFilterItem ForGroup(string id, string name) => new(name, id, IsAll: false, IsUngrouped: false);
}
