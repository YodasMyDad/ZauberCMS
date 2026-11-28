using Blazored.Modal.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Radzen;
using ZauberCMS.Core;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Localization;
using ZauberCMS.Core.Media.Interfaces;
using ZauberCMS.Core.Media.Models;
using ZauberCMS.Core.Media.Parameters;

namespace ZauberCMS.Components.ContextMenus.MediaMenus;

/// <summary>Moves media (a folder with everything in it) to the media recycle bin.</summary>
public class DeleteMediaContextMenu(
    IMediaService mediaService,
    DialogService dialogService,
    NotificationService notificationService,
    IStringLocalizer<ZauberText> text) : ITreeContextMenu
{
    public List<string> Sections => [Constants.Sections.MediaSection];
    public List<string> TreeAlias { get; } = [];
    public string Text(TreeItemContextMenuEventArgs args) => text["general_delete"];
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
            ? text["contextMenus_moveFolderToRecycleBinConfirm"].Value
            : text["contextMenus_moveMediaToRecycleBinConfirm"].Value;
        var delete = await dialogService.Confirm(message, text["contextMenus_deleteMedia"], new ConfirmOptions { OkButtonText = text["general_yes"], CancelButtonText = text["general_no"] });

        if (delete == true)
        {
            // The service raises the media events (ZA-013: this menu raised them a second time)
            var deleteResult = await mediaService.DeleteMediaAsync(new DeleteMediaParameters { MediaId = media.Id });
            if (deleteResult.Success)
            {
                notificationService.ShowSuccessNotification(text["contextMenus_mediaMovedToRecycleBin"]);
            }
            else
            {
                notificationService.ShowErrorNotification(deleteResult.Messages.MessagesAsString());
            }
        }
    }

    public int SortOrder => 100;
}
