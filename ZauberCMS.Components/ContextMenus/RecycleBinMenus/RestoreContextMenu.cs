using Blazored.Modal.Services;
using Microsoft.AspNetCore.Components;
using Radzen;
using ZauberCMS.Core;
using ZauberCMS.Core.Content.Interfaces;
using ZauberCMS.Core.Content.Parameters;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Membership.Interfaces;
using ZauberCMS.Core.Shared;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Components.ContextMenus.RecycleBinMenus;

public class RestoreContextMenu(IContentService contentService, IMembershipService membershipService, AppState appState) : ITreeContextMenu
{
    public List<string> Sections { get; } = [];
    public List<string> TreeAlias { get; } = [Constants.Sections.Trees.RecycleBinTree];
    public string Text(TreeItemContextMenuEventArgs args) => "Restore";

    public string Icon(TreeItemContextMenuEventArgs args) => "settings_backup_restore";

    public string IconColor(TreeItemContextMenuEventArgs args) => string.Empty;

    public bool CanShowContextMenu(TreeItemContextMenuEventArgs args)
    {
        return args.Value is TreeBranch;
    }

    public async Task ContextMenuAction(TreeItemContextMenuEventArgs args, MenuItemEventArgs e, NavigationManager navigationManager,
        ContextMenuService contextMenuService, IModalService modalService)
    {
        var branch = (TreeBranch)args.Value;
        var dbContent = await contentService.GetContentAsync(new GetContentParameters { Id = branch.Id, IncludeChildren = true, IncludeUnpublished = true});
        var currentUser = await membershipService.GetCurrentUser();
        contextMenuService.Close();
        dbContent!.Deleted = false;
        var saveResult = await contentService.SaveContentAsync(new SaveContentParameters { Content = dbContent, ExcludePropertyData = true});
        if (saveResult.Success)
        {
            await appState.NotifyContentChanged(dbContent, currentUser?.Name ?? "Unknown");
        }
    }

    public int SortOrder => -100;
}