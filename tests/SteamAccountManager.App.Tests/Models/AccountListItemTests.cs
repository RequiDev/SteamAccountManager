using System;
using System.Collections.Generic;
using SteamAccountManager.App.Models;
using SteamAccountManager.Core.Models;
using Xunit;

namespace SteamAccountManager.App.Tests.Models;

public class AccountListItemTests
{
    private static SteamAccount Account(
        string id = "76561198000000001",
        string accountName = "alice",
        string personaName = "Alice",
        bool mostRecent = false,
        long timestamp = 1688740727)
        => new()
        {
            SteamId64 = id,
            AccountName = accountName,
            PersonaName = personaName,
            MostRecent = mostRecent,
            Timestamp = timestamp,
        };

    [Fact]
    public void From_CopiesCoreFields_AndMapsActiveFromMostRecent()
    {
        var item = AccountListItem.From(Account(mostRecent: true), metadata: null);

        Assert.Equal("76561198000000001", item.SteamId64);
        Assert.Equal("alice", item.AccountName);
        Assert.Equal("Alice", item.PersonaName);
        Assert.True(item.IsActive);
        Assert.Empty(item.GroupIds);
        Assert.Null(item.CustomLabel);
    }

    [Fact]
    public void DisplayName_PrefersCustomLabel()
    {
        var meta = new AccountMetadata
        {
            SteamId64 = "76561198000000001",
            CustomLabel = "Main",
            Notes = "ranked smurf",
            GroupIds = new List<string> { "g1" },
        };

        var item = AccountListItem.From(Account(personaName: "Alice"), meta);

        Assert.Equal("Main", item.DisplayName);
        Assert.Equal("ranked smurf", item.Notes);
        Assert.Equal(new[] { "g1" }, item.GroupIds);
    }

    [Fact]
    public void Notes_DefaultsToNull_WhenNoMetadata()
    {
        var item = AccountListItem.From(Account(), metadata: null);
        Assert.Null(item.Notes);
    }

    [Fact]
    public void DisplayName_FallsBackToPersona_ThenAccountName()
    {
        var withPersona = AccountListItem.From(Account(personaName: "Alice", accountName: "alice"), metadata: null);
        Assert.Equal("Alice", withPersona.DisplayName);

        var noPersona = AccountListItem.From(Account(personaName: "", accountName: "alice"), metadata: null);
        Assert.Equal("alice", noPersona.DisplayName);

        var emptyLabel = AccountListItem.From(
            Account(personaName: "Alice"),
            new AccountMetadata { SteamId64 = "x", CustomLabel = "   " });
        Assert.Equal("Alice", emptyLabel.DisplayName);
    }

    [Fact]
    public void LastLogin_IsDerivedFromUnixTimestamp()
    {
        var item = AccountListItem.From(Account(timestamp: 1688740727), metadata: null);

        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1688740727), item.LastLogin);
    }

    [Fact]
    public void LastLogin_IsNull_WhenTimestampZero()
    {
        var item = AccountListItem.From(Account(timestamp: 0), metadata: null);
        Assert.Null(item.LastLogin);
    }
}
