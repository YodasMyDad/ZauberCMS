using Blazored.Modal.Services;
using Microsoft.AspNetCore.Components;
using Radzen;
using ZauberCMS.Core;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Media.Interfaces;
using ZauberCMS.Core.Media.Models;
using ZauberCMS.Core.Media.Parameters;

namespace ZauberCMS.Components.ContextMenus.MediaRecycleBinMenus;

/// <summary>
/// Deletes a recycled media item for good, with its file and anything that went to the bin inside it. The
/// confirmation says which pages and members still use it.
/// </summary>
public class DeleteMediaPermanentlyContextMenu(
    IMediaService mediaService,
    DialogService dialogService,
    NotificationService notificationService) : ITreeContextMenu
{
    private const int MaxUsages = 50;

    public List<string> Sections { get; } = [];
    public List<string> TreeAlias { get; } = [Constants.Sections.Trees.MediaRecycleBinTree];
    public string Text(TreeItemContextMenuEventArgs args) => "Delete permanently";
    public string Icon(TreeItemContextMenuEventArgs args) => "dangerous";
    public string IconColor(TreeItemContextMenuEventArgs args) => string.Empty;

    public bool CanShowContextMenu(TreeItemContextMenuEventArgs args) => args.Value is Media;

    public async Task ContextMenuAction(TreeItemContextMenuEventArgs args, MenuItemEventArgs e, NavigationManager navigationManager,
        ContextMenuService contextMenuService, IModalService modalService)
    {
        contextMenuService.Close();
        if (args.Value is not Media media)
        {
            return;
        }

        var usages = await mediaService.GetMediaUsageAsync(new GetMediaUsageParameters
        {
            MediaIds = [media.Id],
            Max = MaxUsages
        });
        var what = media.MediaType == MediaType.Folder ? "this folder, everything in it and their files" : "this media and its file";
        var message = $"{MediaUsageText.Describe(usages, MaxUsages)}Permanently delete {what}? This can't be undone.";

        var confirmed = await dialogService.Confirm(message, $"Delete '{media.Name}'",
            new ConfirmOptions { OkButtonText = "Delete permanently", CancelButtonText = "Cancel" });
        if (confirmed != true)
        {
            return;
        }

        var result = await mediaService.DeleteMediaAsync(new DeleteMediaParameters
        {
            MediaId = media.Id,
            MoveToRecycleBin = false
        });
        if (result.Success)
        {
            notificationService.ShowSuccessNotification($"'{media.Name}' deleted");
        }

        if (!result.Success || result.Messages.Count != 0)
        {
            notificationService.ShowNotifications(result.Messages);
        }
    }

    public int SortOrder => -90;
}
