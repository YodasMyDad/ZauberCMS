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

public class DeleteAllContentContextMenu(
    IContentService contentService, 
    IMembershipService membershipService,
    DialogService dialogService, 
    NotificationService notificationService,
    AppState appState,
    IStringLocalizer<ZauberText> text) : ITreeContextMenu
{
    public List<string> Sections { get; } = [];
    public List<string> TreeAlias { get; } = [Constants.Sections.Trees.RecycleBinTree];
    public string Text(TreeItemContextMenuEventArgs args) => text["contextMenus_deleteAll"];

    public string Icon(TreeItemContextMenuEventArgs args) => "delete_forever";

    public string IconColor(TreeItemContextMenuEventArgs args) => "#e85d5d";

    public bool CanShowContextMenu(TreeItemContextMenuEventArgs args)
    {
        // Only show on the root recycle bin node
        return args.Value is TreeStub stub && stub.Id == Constants.Guids.RecycleBinRootId;
    }

    public async Task ContextMenuAction(TreeItemContextMenuEventArgs args, MenuItemEventArgs e, 
        NavigationManager navigationManager, ContextMenuService contextMenuService, IModalService modalService)
    {
        contextMenuService.Close();
        
        // Every item at the top of the bin, collected before deleting anything (it stopped at 1000 rows before).
        // Deleting an item also deletes the recycled items and blocks below it, so only these are deleted here.
        var topLevelItems = new List<Guid>();
        TreeLevel<Core.Content.Models.Content> level;
        do
        {
            level = await contentService.GetContentTreeLevelAsync(new ContentTreeLevelParameters
            {
                InRecycleBin = true,
                Skip = topLevelItems.Count,
                Take = 500
            });
            topLevelItems.AddRange(level.Items.Select(x => x.Id));
        } while (level.HasMore && level.Items.Count > 0);

        if (topLevelItems.Count == 0)
        {
            notificationService.Notify(new NotificationMessage
            {
                Severity = NotificationSeverity.Info,
                Summary = text["contextMenus_recycleBinEmpty"],
                Detail = text["contextMenus_noItemsToDelete"],
                Duration = 4000
            });
            return;
        }

        // Confirm deletion
        var confirmMessage = text["contextMenus_deleteAllContentConfirm", topLevelItems.Count].Value;
        var confirmed = await dialogService.Confirm(confirmMessage, text["contextMenus_deleteAll"], 
            new ConfirmOptions { OkButtonText = text["contextMenus_deleteAll"], CancelButtonText = text["general_cancel"] });
        
        if (confirmed != true)
            return;

        var currentUser = await membershipService.GetCurrentUser();
        var successCount = 0;
        var errorCount = 0;

        // Delete all items
        foreach (var contentId in topLevelItems)
        {
            var result = await contentService.DeleteContentAsync(new DeleteContentParameters
            {
                ContentId = contentId
            });
            
            if (result.Success)
            {
                successCount++;
            }
            else
            {
                errorCount++;
            }
        }

        // Notify state change once after all deletions
        await appState.NotifyContentChanged(null, currentUser?.Name ?? "Unknown");

        // Show result notification
        if (errorCount == 0)
        {
            notificationService.Notify(new NotificationMessage
            {
                Severity = NotificationSeverity.Success,
                Summary = text["general_success"],
                Detail = text["contextMenus_recycleBinItemsDeleted", successCount],
                Duration = 4000
            });
        }
        else
        {
            notificationService.Notify(new NotificationMessage
            {
                Severity = NotificationSeverity.Warning,
                Summary = text["contextMenus_partiallyCompleted"],
                Detail = text["contextMenus_recycleBinItemsPartlyDeleted", successCount, errorCount],
                Duration = 6000
            });
        }
    }

    public int SortOrder => -80; // Show after restore but before individual delete
}

