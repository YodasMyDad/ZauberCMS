using Blazored.Modal.Services;
using Microsoft.AspNetCore.Components;
using Radzen;
using ZauberCMS.Core;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Media.Interfaces;
using ZauberCMS.Core.Media.Parameters;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Components.ContextMenus.MediaRecycleBinMenus;

/// <summary>Permanently deletes everything in the media recycle bin, with the files.</summary>
public class EmptyMediaRecycleBinContextMenu(
    IMediaService mediaService,
    DialogService dialogService,
    NotificationService notificationService) : ITreeContextMenu
{
    private const int MaxUsages = 50;

    public List<string> Sections { get; } = [];
    public List<string> TreeAlias { get; } = [Constants.Sections.Trees.MediaRecycleBinTree];
    public string Text(TreeItemContextMenuEventArgs args) => "Empty recycle bin";
    public string Icon(TreeItemContextMenuEventArgs args) => "delete_forever";
    public string IconColor(TreeItemContextMenuEventArgs args) => "#e85d5d";

    public bool CanShowContextMenu(TreeItemContextMenuEventArgs args) =>
        args.Value is TreeStub stub && stub.Id == Constants.Guids.MediaRecycleBinRootId;

    public async Task ContextMenuAction(TreeItemContextMenuEventArgs args, MenuItemEventArgs e, NavigationManager navigationManager,
        ContextMenuService contextMenuService, IModalService modalService)
    {
        contextMenuService.Close();

        // Only the count is needed here; the usage check looks at every binned item on the server
        var binned = await mediaService.QueryMediaAsync(new QueryMediaParameters
        {
            WhereClause = x => x.Deleted,
            IncludeDeleted = true,
            AmountPerPage = 1
        });
        var count = binned.TotalItems;
        if (count == 0)
        {
            notificationService.ShowNotification("The recycle bin is empty", NotificationSeverity.Info);
            return;
        }

        var usages = await mediaService.GetMediaUsageAsync(new GetMediaUsageParameters
        {
            AllInRecycleBin = true,
            IncludeDescendants = false,
            Max = MaxUsages
        });
        var message = $"{MediaUsageText.Describe(usages, MaxUsages)}Permanently delete all {count} items in the media recycle bin and their files? This can't be undone.";
        var confirmed = await dialogService.Confirm(message, "Empty recycle bin",
            new ConfirmOptions { OkButtonText = "Empty recycle bin", CancelButtonText = "Cancel" });
        if (confirmed != true)
        {
            return;
        }

        var result = await mediaService.EmptyMediaRecycleBinAsync(new EmptyMediaRecycleBinParameters());
        if (result.Success)
        {
            notificationService.ShowSuccessNotification($"{result.Entity} items deleted from the recycle bin");
        }

        if (!result.Success || result.Messages.Count != 0)
        {
            notificationService.ShowNotifications(result.Messages);
        }
    }

    public int SortOrder => -80;
}
