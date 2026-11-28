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

namespace ZauberCMS.Components.ContextMenus.MediaRecycleBinMenus;

/// <summary>
/// Deletes a recycled media item for good, with its file and anything that went to the bin inside it. The
/// confirmation says which pages and members still use it.
/// </summary>
public class DeleteMediaPermanentlyContextMenu(
    IMediaService mediaService,
    DialogService dialogService,
    NotificationService notificationService,
    IStringLocalizer<ZauberText> text) : ITreeContextMenu
{
    private const int MaxUsages = 50;

    public List<string> Sections { get; } = [];
    public List<string> TreeAlias { get; } = [Constants.Sections.Trees.MediaRecycleBinTree];
    public string Text(TreeItemContextMenuEventArgs args) => text["contextMenus_deletePermanently"];
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
        var usedBy = MediaUsageText.Describe(usages, MaxUsages, text);
        var message = media.MediaType == MediaType.Folder
            ? text["contextMenus_deleteFolderPermanentlyConfirm", usedBy].Value
            : text["contextMenus_deleteMediaPermanentlyConfirm", usedBy].Value;

        var confirmed = await dialogService.Confirm(message, text["contextMenus_deleteItem", media.Name ?? string.Empty],
            new ConfirmOptions { OkButtonText = text["contextMenus_deletePermanently"], CancelButtonText = text["general_cancel"] });
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
            notificationService.ShowSuccessNotification(text["contextMenus_itemDeleted", media.Name ?? string.Empty]);
        }

        if (!result.Success || result.Messages.Count != 0)
        {
            notificationService.ShowNotifications(result.Messages, text);
        }
    }

    public int SortOrder => -90;
}
