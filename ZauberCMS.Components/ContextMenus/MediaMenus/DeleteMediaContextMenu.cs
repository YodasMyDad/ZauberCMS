using Blazored.Modal.Services;
using Microsoft.AspNetCore.Components;
using Radzen;
using ZauberCMS.Core;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Media.Interfaces;
using ZauberCMS.Core.Media.Models;
using ZauberCMS.Core.Media.Parameters;

namespace ZauberCMS.Components.ContextMenus.MediaMenus;

/// <summary>Moves media (a folder with everything in it) to the media recycle bin.</summary>
public class DeleteMediaContextMenu(
    IMediaService mediaService,
    DialogService dialogService,
    NotificationService notificationService) : ITreeContextMenu
{
    public List<string> Sections => [Constants.Sections.MediaSection];
    public List<string> TreeAlias { get; } = [];
    public string Text(TreeItemContextMenuEventArgs args) => "Delete";
    public string Icon(TreeItemContextMenuEventArgs args) => "close";
    public string IconColor(TreeItemContextMenuEventArgs args) => string.Empty;
    public bool CanShowContextMenu(TreeItemContextMenuEventArgs args)
    {
        return args.Value is Media;
    }

    public async Task ContextMenuAction(TreeItemContextMenuEventArgs args, MenuItemEventArgs e, NavigationManager navigationManager,
        ContextMenuService contextMenuService, IModalService modalService)
    {
        var media = (Media)args.Value!;
        var message = media.MediaType == MediaType.Folder
            ? "Move this folder and everything in it to the recycle bin?"
            : "Move this media to the recycle bin?";
        var delete = await dialogService.Confirm(message, "Delete Media", new ConfirmOptions { OkButtonText = "Yes", CancelButtonText = "No" });

        if (delete == true)
        {
            // The service raises the media events (ZA-013: this menu raised them a second time)
            var deleteResult = await mediaService.DeleteMediaAsync(new DeleteMediaParameters { MediaId = media.Id });
            if (deleteResult.Success)
            {
                notificationService.ShowSuccessNotification("Moved to the recycle bin");
            }
            else
            {
                notificationService.ShowErrorNotification(deleteResult.Messages.MessagesAsString());
            }
        }
    }

    public int SortOrder => 100;
}
