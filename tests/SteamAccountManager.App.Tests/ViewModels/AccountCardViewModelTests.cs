using System;
using System.Threading.Tasks;
using SteamAccountManager.App.Models;
using SteamAccountManager.App.Tests.Fakes;
using SteamAccountManager.App.ViewModels;
using Xunit;

namespace SteamAccountManager.App.Tests.ViewModels;

public class AccountCardViewModelTests
{
    private static AccountListItem Item(
        string id = "1", bool active = false, string? label = "Main", string? notes = null)
        => new(id, "alice", "Alice", label, new[] { "g1" }, active, DateTimeOffset.FromUnixTimeSeconds(1688740727))
        { Notes = notes };

    private static AccountCardViewModel Build(
        out FakeAccountSwitcher switcher, out FakeGroupManagementService groups, AccountListItem? item = null)
    {
        switcher = new FakeAccountSwitcher();
        groups = new FakeGroupManagementService();
        return new AccountCardViewModel(item ?? Item(), switcher, groups);
    }

    [Fact]
    public void ExposesProjectedFields()
    {
        var vm = Build(out _, out _, Item(active: true));

        Assert.Equal("1", vm.SteamId64);
        Assert.Equal("Main", vm.DisplayName);
        Assert.Equal("alice", vm.AccountName);
        Assert.True(vm.IsActive);
        Assert.Equal(new[] { "g1" }, vm.GroupIds);
        Assert.False(vm.IsBusy);
        Assert.False(vm.IsEditing);
    }

    [Fact]
    public void AvatarPath_IsSettable_AndRaisesChange()
    {
        var vm = Build(out _, out _);
        var raised = false;
        vm.PropertyChanged += (_, e) => raised |= e.PropertyName == nameof(vm.AvatarPath);

        vm.AvatarPath = @"C:\cache\1.jpg";

        Assert.Equal(@"C:\cache\1.jpg", vm.AvatarPath);
        Assert.True(raised);
    }

    [Fact]
    public async Task SwitchCommand_DelegatesToSwitcher_OffUiThread()
    {
        var vm = Build(out var switcher, out _);

        await vm.SwitchCommand.ExecuteAsync(null);

        Assert.Equal(new[] { "1" }, switcher.SwitchedTo.ToArray());
        Assert.False(vm.IsBusy); // reset after completion
    }

    [Fact]
    public async Task SwitchCommand_RaisesSwitchCompleted()
    {
        var vm = Build(out _, out _);
        string? completedId = null;
        vm.SwitchCompleted += (_, id) => completedId = id;

        await vm.SwitchCommand.ExecuteAsync(null);

        Assert.Equal("1", completedId);
    }

    [Fact]
    public async Task SwitchCommand_SetsErrorMessage_OnFailure()
    {
        var failing = new ThrowingAccountSwitcher();
        var vm = new AccountCardViewModel(Item(), failing, new FakeGroupManagementService());

        await vm.SwitchCommand.ExecuteAsync(null);

        Assert.False(vm.IsBusy);
        Assert.NotNull(vm.ErrorMessage);
    }

    [Fact]
    public void BeginEditCommand_SeedsEditableFields_AndEntersEditMode()
    {
        var vm = Build(out _, out _, Item(label: "Main", notes: "ranked smurf"));

        vm.BeginEditCommand.Execute(null);

        Assert.True(vm.IsEditing);
        Assert.Equal("Main", vm.EditableLabel);
        Assert.Equal("ranked smurf", vm.EditableNotes);
    }

    [Fact]
    public void SaveLabelCommand_PersistsViaService_ExitsEditMode_AndRaisesChanged()
    {
        var vm = Build(out _, out var groups);
        var changedId = (string?)null;
        vm.MetadataChanged += (_, id) => changedId = id;

        vm.BeginEditCommand.Execute(null);
        vm.EditableLabel = "New label";
        vm.EditableNotes = "New notes";
        vm.SaveLabelCommand.Execute(null);

        Assert.Equal(new[] { ("1", (string?)"New label", (string?)"New notes") }, groups.LabelNotesCalls.ToArray());
        Assert.False(vm.IsEditing);
        Assert.Equal("New label", vm.DisplayName); // local DisplayName reflects the new label
        Assert.Equal("1", changedId);
    }

    [Fact]
    public void SaveLabelCommand_ClearingLabel_RevertsDisplayNameToPersonaName()
    {
        var vm = Build(out _, out var groups, Item(label: "Main")); // PersonaName "Alice"

        vm.BeginEditCommand.Execute(null);
        vm.EditableLabel = "   "; // cleared
        vm.SaveLabelCommand.Execute(null);

        Assert.Equal(new[] { ("1", (string?)null, (string?)null) }, groups.LabelNotesCalls.ToArray());
        Assert.Equal("Alice", vm.DisplayName); // falls back to persona name
    }

    [Fact]
    public void CancelEditCommand_DiscardsChanges_AndExitsEditMode()
    {
        var vm = Build(out _, out var groups, Item(label: "Main"));

        vm.BeginEditCommand.Execute(null);
        vm.EditableLabel = "scratch";
        vm.CancelEditCommand.Execute(null);

        Assert.False(vm.IsEditing);
        Assert.Empty(groups.LabelNotesCalls);
        Assert.Equal("Main", vm.DisplayName); // unchanged
    }
}
