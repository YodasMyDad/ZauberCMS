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

/// <summary>Takes media out of the recycle bin, with what went to the bin with it.</summary>
public class RestoreMediaContextMenu(IMediaService mediaService, NotificationService notificationService, IStringLocalizer<ZauberText> text) : ITreeContextMenu
{
    public List<string> Sections { get; } = [];
    public List<string> TreeAlias { get; } = [Constants.Sections.Trees.MediaRecycleBinTree];
    public string Text(TreeItemContextMenuEventArgs args) => text["general_restore"];
    public string Icon(TreeItemContextMenuEventArgs args) => "settings_backup_restore";
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

        // The service raises the media events, so both trees refresh
        var result = await mediaService.RestoreMediaAsync(new RestoreMediaParameters { MediaId = media.Id });
        if (result.Success)
        {
            notificationService.ShowSuccessNotification(text["contextMenus_itemRestored", media.Name ?? string.Empty]);
        }

        if (!result.Success || result.Messages.Count != 0)
        {
            notificationService.ShowNotifications(result.Messages, text);
        }
    }

    public int SortOrder => -100;
}
