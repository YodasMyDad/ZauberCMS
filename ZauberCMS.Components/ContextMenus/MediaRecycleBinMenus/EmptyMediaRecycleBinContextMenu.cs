using Blazored.Modal.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Radzen;
using ZauberCMS.Core;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Localization;
using ZauberCMS.Core.Media.Interfaces;
using ZauberCMS.Core.Media.Parameters;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Components.ContextMenus.MediaRecycleBinMenus;

/// <summary>Permanently deletes everything in the media recycle bin, with the files.</summary>
public class EmptyMediaRecycleBinContextMenu(
    IMediaService mediaService,
    DialogService dialogService,
    NotificationService notificationService,
    IStringLocalizer<ZauberText> text) : ITreeContextMenu
{
    private const int MaxUsages = 50;

    public List<string> Sections { get; } = [];
    public List<string> TreeAlias { get; } = [Constants.Sections.Trees.MediaRecycleBinTree];
    public string Text(TreeItemContextMenuEventArgs args) => text["contextMenus_emptyRecycleBin"];
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
            notificationService.ShowNotification(text["contextMenus_recycleBinIsEmpty"], NotificationSeverity.Info);
            return;
        }

        var usages = await mediaService.GetMediaUsageAsync(new GetMediaUsageParameters
        {
            AllInRecycleBin = true,
            IncludeDescendants = false,
            Max = MaxUsages
        });
        var message = text["contextMenus_emptyMediaRecycleBinConfirm", MediaUsageText.Describe(usages, MaxUsages, text), count].Value;
        var confirmed = await dialogService.Confirm(message, text["contextMenus_emptyRecycleBin"],
            new ConfirmOptions { OkButtonText = text["contextMenus_emptyRecycleBin"], CancelButtonText = text["general_cancel"] });
        if (confirmed != true)
        {
            return;
        }

        var result = await mediaService.EmptyMediaRecycleBinAsync(new EmptyMediaRecycleBinParameters());
        if (result.Success)
        {
            notificationService.ShowSuccessNotification(text["contextMenus_mediaRecycleBinEmptied", result.Entity ?? 0]);
        }

        if (!result.Success || result.Messages.Count != 0)
        {
            notificationService.ShowNotifications(result.Messages, text);
        }
    }

    public int SortOrder => -80;
}
