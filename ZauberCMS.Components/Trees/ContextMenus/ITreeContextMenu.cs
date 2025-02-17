using Blazored.Modal.Services;
using Microsoft.AspNetCore.Components;
using Radzen;

namespace ZauberCMS.Components.Trees.ContextMenus;

public interface ITreeContextMenu
{
    List<string> Sections { get; }
    List<string> TreeAlias { get; }
    string Text(TreeItemContextMenuEventArgs args);
    string Icon(TreeItemContextMenuEventArgs args);
    string IconColor(TreeItemContextMenuEventArgs args);
    bool CanShowContextMenu(TreeItemContextMenuEventArgs args);
    Task ContextMenuAction(TreeItemContextMenuEventArgs args, MenuItemEventArgs e, NavigationManager navigationManager, ContextMenuService contextMenuService, IModalService modalService);
    int SortOrder { get; }
}


/*Func<TreeItemContextMenuEventArgs, bool> CanShowContextMenu();*/
/*Func<TreeItemContextMenuEventArgs, MenuItemEventArgs, NavigationManager, ContextMenuService, IModalService, Task> ContextMenuAction();*/