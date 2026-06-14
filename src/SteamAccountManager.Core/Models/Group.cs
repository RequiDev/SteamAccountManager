namespace SteamAccountManager.Core.Models;

/// <summary>A user-defined category. Many accounts can belong to many groups.</summary>
public sealed class Group
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public int SortOrder { get; set; }
}
