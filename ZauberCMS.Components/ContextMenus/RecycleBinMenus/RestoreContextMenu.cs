using Blazored.Modal.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Radzen;
using ZauberCMS.Core;
using ZauberCMS.Core.Content.Interfaces;
using ZauberCMS.Core.Content.Parameters;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Localization;
using ZauberCMS.Core.Membership.Interfaces;
using ZauberCMS.Core.Shared;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Components.ContextMenus.RecycleBinMenus;

public class RestoreContextMenu(IContentService contentService, IMembershipService membershipService, AppState appState,
    NotificationService notificationService, IStringLocalizer<ZauberText> text) : ITreeContextMenu
{
    public List<string> Sections { get; } = [];
    public List<string> TreeAlias { get; } = [Constants.Sections.Trees.RecycleBinTree];
    public string Text(TreeItemContextMenuEventArgs args) => text["general_restore"];

    public string Icon(TreeItemContextMenuEventArgs args) => "settings_backup_restore";

    public string IconColor(TreeItemContextMenuEventArgs args) => string.Empty;

    public bool CanShowContextMenu(TreeItemContextMenuEventArgs args)
    {
        return args.Value is TreeBranch;
    }

    public async Task ContextMenuAction(TreeItemContextMenuEventArgs args, MenuItemEventArgs e, NavigationManager navigationManager,
        ContextMenuService contextMenuService, IModalService modalService)
    {
        if (args.Value is not TreeBranch branch)
        {
            return;
        }

        var dbContent = await contentService.GetContentAsync(new GetContentParameters { Id = branch.Id, IncludeChildren = true, IncludeUnpublished = true, IncludeDeleted = true });
        if (dbContent is null)
        {
            return;
        }

        var currentUser = await membershipService.GetCurrentUser();
        contextMenuService.Close();
        dbContent.Deleted = false;
        // Also restores the items that went to the bin with this one; a URL taken in the meantime gets a suffix
        var saveResult = await contentService.SaveContentAsync(new SaveContentParameters { Content = dbContent, ExcludePropertyData = true });
        if (saveResult.Success)
        {
            await appState.NotifyContentChanged(dbContent, currentUser?.Name ?? "Unknown");
        }

        if (!saveResult.Success || saveResult.Messages.Count != 0)
        {
            notificationService.ShowNotifications(saveResult.Messages, text);
        }
    }

    public int SortOrder => -100;
}