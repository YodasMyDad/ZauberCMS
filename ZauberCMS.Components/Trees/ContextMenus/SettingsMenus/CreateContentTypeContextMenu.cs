using Blazored.Modal.Services;
using Microsoft.AspNetCore.Components;
using Radzen;
using ZauberCMS.Core;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Components.Trees.ContextMenus.SettingsMenus;

public class CreateContentTypeContextMenu() : ITreeContextMenu
{
    public List<string> Sections { get; } = [];
    public List<string> TreeAlias { get; } = [Constants.Sections.Trees.SettingsStructureTree];
    public string Text(TreeItemContextMenuEventArgs args) => "Create";

    public string Icon(TreeItemContextMenuEventArgs args) => "add";

    public string IconColor(TreeItemContextMenuEventArgs args) => string.Empty;

    public bool CanShowContextMenu(TreeItemContextMenuEventArgs args)
    {
        if (args.Value is TreeStub treeStub)
        {
            if (treeStub.StubType == typeof(ContentType))
            {
                return true;
            }
        }
        return false;
    }

    public Task ContextMenuAction(TreeItemContextMenuEventArgs args, MenuItemEventArgs e, NavigationManager navigationManager,
        ContextMenuService contextMenuService, IModalService modalService)
    {
        contextMenuService.Close();
        navigationManager.NavigateTo("/admin/createcontentype");
        return Task.CompletedTask;
    }

    public int SortOrder => -90;
}