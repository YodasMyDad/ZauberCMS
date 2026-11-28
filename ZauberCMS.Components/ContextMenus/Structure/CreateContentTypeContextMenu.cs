using Blazored.Modal.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Radzen;
using ZauberCMS.Core;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Localization;

namespace ZauberCMS.Components.ContextMenus.Structure;

public class CreateContentTypeContextMenu(IStringLocalizer<ZauberText> text) : ITreeContextMenu
{
    public List<string> Sections { get; } = [];
    public List<string> TreeAlias { get; } = [Constants.Sections.Trees.StructureContentTypeTree, Constants.Sections.Trees.StructureElementTypeTree, Constants.Sections.Trees.StructureCompositionsTree];

    public string Text(TreeItemContextMenuEventArgs args)
    {
        if (args.Value is ContentType contentType)
        {
            if (contentType.IsElementType)
            {
                return text["contextMenus_createElementType"];
            }

            if (contentType.IsComposition)
            {
                return text["contextMenus_createComposition"];
            }
        }
        return text["contextMenus_createContentType"];
    }

    public string Icon(TreeItemContextMenuEventArgs args) => "add";

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
        navigationManager.NavigateTo($"{Urls.AdminStructureCreateContentType}/{contentType.Id}");
        return Task.CompletedTask;
    }

    public int SortOrder => -100;
}