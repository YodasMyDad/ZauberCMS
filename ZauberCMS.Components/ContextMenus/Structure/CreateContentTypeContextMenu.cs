using Blazored.Modal.Services;
using Microsoft.AspNetCore.Components;
using Radzen;
using ZauberCMS.Components.Trees.ContextMenus;
using ZauberCMS.Core;
using ZauberCMS.Core.Content.Models;

namespace ZauberCMS.Components.ContextMenus.Structure;

public class CreateContentTypeContextMenu() : ITreeContextMenu
{
    public List<string> Sections { get; } = [];
    public List<string> TreeAlias { get; } = [Constants.Sections.Trees.StructureContentTypeTree, Constants.Sections.Trees.StructureElementTypeTree];
    public string Text(TreeItemContextMenuEventArgs args) => "Create";

    public string Icon(TreeItemContextMenuEventArgs args) => "add";

    public string IconColor(TreeItemContextMenuEventArgs args) => string.Empty;

    public bool CanShowContextMenu(TreeItemContextMenuEventArgs args)
    {
        return args.Value is ContentType;
    }

    public Task ContextMenuAction(TreeItemContextMenuEventArgs args, MenuItemEventArgs e, NavigationManager navigationManager,
        ContextMenuService contextMenuService, IModalService modalService)
    {
        contextMenuService.Close();
        navigationManager.NavigateTo(Urls.AdminSettingsCreateContentType);
        return Task.CompletedTask;
    }

    public int SortOrder => -90;
}