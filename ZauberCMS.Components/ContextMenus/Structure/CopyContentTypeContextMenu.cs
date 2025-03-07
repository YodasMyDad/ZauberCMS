using Blazored.Modal.Services;
using Microsoft.AspNetCore.Components;
using Radzen;
using ZauberCMS.Core;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Components.ContextMenus.Structure;

public class CopyContentTypeContextMenu() : ITreeContextMenu
{
    public List<string> Sections { get; } = [];
    public List<string> TreeAlias { get; } = [Constants.Sections.Trees.StructureContentTypeTree, Constants.Sections.Trees.StructureElementTypeTree, Constants.Sections.Trees.StructureCompositionsTree];
    public string Text(TreeItemContextMenuEventArgs args) => "Copy";

    public string Icon(TreeItemContextMenuEventArgs args) => "content_copy";

    public string IconColor(TreeItemContextMenuEventArgs args) => string.Empty;

    public bool CanShowContextMenu(TreeItemContextMenuEventArgs args)
    {
        if (args.Value is ContentType contentType)
        {
            return !contentType.IsFolder;
        }

        return false;
    }

    public Task ContextMenuAction(TreeItemContextMenuEventArgs args, MenuItemEventArgs e, NavigationManager navigationManager,
        ContextMenuService contextMenuService, IModalService modalService)
    {
        var branch = (ContentType)args.Value;
        contextMenuService.Close();
        navigationManager.NavigateTo($"{Urls.AdminStructureCopyContentType}/{branch.Id}");
        return Task.CompletedTask;
    }

    public int SortOrder => -90;
}