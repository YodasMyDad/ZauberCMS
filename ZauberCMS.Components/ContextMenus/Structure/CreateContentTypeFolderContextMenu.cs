using Blazored.Modal.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Radzen;
using ZauberCMS.Core;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Localization;

namespace ZauberCMS.Components.ContextMenus.Structure;

public class CreateContentTypeFolderContextMenu(IStringLocalizer<ZauberText> text) : ITreeContextMenu
{
    public List<string> Sections { get; } = [];
    public List<string> TreeAlias { get; } = [Constants.Sections.Trees.StructureContentTypeTree, Constants.Sections.Trees.StructureElementTypeTree, Constants.Sections.Trees.StructureCompositionsTree];
    public string Text(TreeItemContextMenuEventArgs args) => text["contextMenus_createFolder"];

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
        if (args.Value is not ContentType contentType)
        {
            return Task.CompletedTask;
        }

        contextMenuService.Close();
        navigationManager.NavigateTo(navigationManager.ToSiteUrl($"{Urls.AdminStructureCreateFolderWithParent}/{contentType.Id}"));
        return Task.CompletedTask;
    }

    public int SortOrder => -99;
}