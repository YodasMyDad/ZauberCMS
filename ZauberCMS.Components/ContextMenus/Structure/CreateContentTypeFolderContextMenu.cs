using Blazored.Modal.Services;
using Microsoft.AspNetCore.Components;
using Radzen;
using ZauberCMS.Core;
using ZauberCMS.Core.Content.Models;

namespace ZauberCMS.Components.ContextMenus.Structure;

public class CreateContentTypeFolderContextMenu : ITreeContextMenu
{
    public List<string> Sections { get; } = [];
    public List<string> TreeAlias { get; } = [Constants.Sections.Trees.StructureContentTypeTree, Constants.Sections.Trees.StructureElementTypeTree, Constants.Sections.Trees.StructureCompositionsTree];
    public string Text(TreeItemContextMenuEventArgs args) => "Create Folder";

    public string Icon(TreeItemContextMenuEventArgs args) => "folder";

    public string IconColor(TreeItemContextMenuEventArgs args) => string.Empty;

    public bool CanShowContextMenu(TreeItemContextMenuEventArgs args)
    {
        if (args.Value is ContentType contentType)
        {
            return contentType.IsFolder;
        }
        return false;
    }


    public Task ContextMenuAction(TreeItemContextMenuEventArgs args, MenuItemEventArgs e, NavigationManager navigationManager,
        ContextMenuService contextMenuService, IModalService modalService)
    {
        var contentType = (ContentType)args.Value;
        contextMenuService.Close();
        navigationManager.NavigateTo($"{Urls.AdminStructureCreateFolderWithParent}/{contentType.Id}");
        return Task.CompletedTask;
    }

    public int SortOrder => -99;
}