using Blazored.Modal.Services;
using Microsoft.AspNetCore.Components;
using Radzen;
using ZauberCMS.Core;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Components.Trees.ContextMenus.SettingsMenus;

public class CopyContentTypeContextMenu() : ITreeContextMenu
{
    public List<string> Sections { get; } = [];
    public List<string> TreeAlias { get; } = [Constants.Sections.Trees.SettingsStructureTree];
    public string Text(TreeItemContextMenuEventArgs args) => "Copy";

    public string Icon(TreeItemContextMenuEventArgs args) => "content_copy";

    public string IconColor(TreeItemContextMenuEventArgs args) => string.Empty;

    public bool CanShowContextMenu(TreeItemContextMenuEventArgs args)
    {
        if (args.Value is TreeBranch treeBranch)
        {
            if (treeBranch.BranchType == typeof(ContentType))
            {
                return true;
            }
        }
        return false;
    }

    public Task ContextMenuAction(TreeItemContextMenuEventArgs args, MenuItemEventArgs e, NavigationManager navigationManager,
        ContextMenuService contextMenuService, IModalService modalService)
    {
        var branch = (TreeBranch)args.Value;
        contextMenuService.Close();
        navigationManager.NavigateTo($"/admin/copycontentype/{branch.Id}");
        return Task.CompletedTask;
    }

    public int SortOrder => -100;
}