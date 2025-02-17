using Blazored.Modal.Services;
using Microsoft.AspNetCore.Components;
using Radzen;

namespace ZauberCMS.Components.Trees.ContextMenus;

public interface ITreeContextMenu
{
    List<string> Sections { get; }
    List<string> TreeAlias { get; }
    List<ContextMenuItem> ContentMenuItems(TreeItemContextMenuEventArgs args);
    Task ContextMenuEvents(TreeItemContextMenuEventArgs args, 
        MenuItemEventArgs e,
        NavigationManager navigationManager, 
        ContextMenuService contextMenuService,
        IModalService modalService);
    int SortOrder { get; }
}