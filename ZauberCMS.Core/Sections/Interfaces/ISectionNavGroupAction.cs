using Blazored.Modal.Services;
using Microsoft.AspNetCore.Components;
using Radzen;

namespace ZauberCMS.Core.Sections.Interfaces;

public interface ISectionNavGroupAction
{
    public string Text { get; }
    public string Icon { get; }
    public string IconColor { get; }
    public string SectionNavGroupAlias { get; }
    Task ContextMenuAction(MenuItemEventArgs e, NavigationManager navigationManager, ContextMenuService contextMenuService, IModalService modalService);
}

/*void ShowContextMenuWithItems(MouseEventArgs args)
{
    ContextMenuService.Open(args,
        new List<ContextMenuItem> {
            new ContextMenuItem(){ Text = "Context menu item 1", Value = 1, Icon = "home" },
            new ContextMenuItem(){ Text = "Context menu item 2", Value = 2, Icon = "search", Disabled = true },
            new ContextMenuItem(){ Text = "Context menu item 3", Value = 3, Icon = "info" },
        }, OnMenuItemClick);
}

void OnMenuItemClick(MenuItemEventArgs args)
{
    console.Log($"Menu item with Value={args.Value} clicked");
    if(!args.Value.Equals(3) && !args.Value.Equals(4))
    {
        ContextMenuService.Close();
    }
}*/