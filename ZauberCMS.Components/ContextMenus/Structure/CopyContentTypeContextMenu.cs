using Blazored.Modal.Services;
using Microsoft.AspNetCore.Components;
using Radzen;
using ZauberCMS.Components.Trees.ContextMenus;
using ZauberCMS.Core;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Components.ContextMenus.Structure;

public class CopyContentTypeContextMenu() : ITreeContextMenu
{
    public List<string> Sections { get; } = [];
    public List<string> TreeAlias { get; } = [Constants.Sections.Trees.StructureContentTypeTree, Constants.Sections.Trees.StructureElementTypeTree];
    public string Text(TreeItemContextMenuEventArgs args) => "Copy";

    public string Icon(TreeItemContextMenuEventArgs args) => "content_copy";

    public string IconColor(TreeItemContextMenuEventArgs args) => string.Empty;

    public bool CanShowContextMenu(TreeItemContextMenuEventArgs args)
    {
        return args.Value is ContentType;
    }

    public Task ContextMenuAction(TreeItemContextMenuEventArgs args, MenuItemEventArgs e, NavigationManager navigationManager,
        ContextMenuService contextMenuService, IModalService modalService)
    {
        var branch = (ContentType)args.Value;
        contextMenuService.Close();
        navigationManager.NavigateTo($"{Urls.AdminSettingsCopyContentType}/{branch.Id}");
        return Task.CompletedTask;
    }

    public int SortOrder => -100;
}