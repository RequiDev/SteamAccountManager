using System.Windows.Controls;
using SteamAccountManager.App.ViewModels;

namespace SteamAccountManager.App;

/// <summary>Rebuilds the tray context menu items from the <see cref="TrayViewModel"/>.</summary>
internal static class TrayMenuBuilder
{
    public static void Build(ContextMenu menu, TrayViewModel vm)
    {
        vm.Rebuild();
        menu.Items.Clear();

        // Title (disabled header).
        menu.Items.Add(new MenuItem { Header = "Steam Account Manager", IsEnabled = false });
        menu.Items.Add(new Separator());

        // One submenu per group, each account a clickable (checkable=active) leaf.
        foreach (var group in vm.Groups)
        {
            var groupItem = new MenuItem { Header = group.Header };
            foreach (var account in group.Accounts)
            {
                var leaf = new MenuItem
                {
                    Header = account.DisplayName,
                    IsCheckable = true, // so the active account shows a checkmark
                    IsChecked = account.IsActive,
                    Command = vm.SwitchAccountCommand,
                    CommandParameter = account.SteamId64,
                };
                groupItem.Items.Add(leaf);
            }

            menu.Items.Add(groupItem);
        }

        menu.Items.Add(new Separator());
        menu.Items.Add(new MenuItem { Header = "Open", Command = vm.ShowWindowCommand });
        menu.Items.Add(new MenuItem
        {
            Header = "Start with Windows",
            IsCheckable = true,
            IsChecked = vm.IsAutostartEnabled,
            Command = vm.ToggleAutostartCommand,
        });
        menu.Items.Add(new Separator());
        menu.Items.Add(new MenuItem { Header = "Exit", Command = vm.ExitCommand });
    }
}
