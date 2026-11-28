using Blazored.Modal;
using Blazored.Modal.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Radzen;
using ZauberCMS.Components.Admin.MediaSection.Dialogs;
using ZauberCMS.Core;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Localization;
using ZauberCMS.Core.Media.Interfaces;
using ZauberCMS.Core.Media.Models;
using ZauberCMS.Core.Media.Parameters;
using ZauberCMS.Core.Membership.Interfaces;
using ZauberCMS.Core.Shared;
using ZauberCMS.Core.Shared.Interfaces;

namespace ZauberCMS.Components.ContextMenus.MediaMenus;

public class MoveMediaContextMenu(NotificationService notificationService, IMembershipService membershipService, IMediaService mediaService, AppState appState, IStringLocalizer<ZauberText> text) : ITreeContextMenu
{
    public List<string> Sections => [Constants.Sections.MediaSection];
    public List<string> TreeAlias => [];
    public string Text(TreeItemContextMenuEventArgs args) => text["general_move"];
    public string Icon(TreeItemContextMenuEventArgs args) => "move_up";
    public string IconColor(TreeItemContextMenuEventArgs args) => string.Empty;
    public bool CanShowContextMenu(TreeItemContextMenuEventArgs args)
    {
        if(args.Value is Content content)
        {
            return content.Published;
        }

        return args.Value is Media;
    }

    private IModalReference? Modal { get; set; }
    
    public async Task ContextMenuAction(TreeItemContextMenuEventArgs args, MenuItemEventArgs e, NavigationManager navigationManager,
        ContextMenuService contextMenuService, IModalService modalService)
    {
        contextMenuService.Close();
        var baseItem = (IBaseItem)args.Value!;

        var parameters = new Dictionary<string, object>
        {
            { nameof(MoveMedia.Item), baseItem}
        };
        if (baseItem.ParentId != null)
        {
            parameters.Add(nameof(MoveMedia.ParentId), baseItem.ParentId);
        }

        Modal = modalService.OpenSidePanel<MoveMedia>(args.Value is Content ? text["contextMenus_moveContent"] : text["contextMenus_moveMedia"], parameters);
        var result = await Modal.Result;
        if (result is { Confirmed: true, Data: Guid parentId })
        {
            if (parentId == Guid.Empty)
            {
                baseItem.ParentId = null;
            }
            else
            {
                baseItem.ParentId = parentId;
            }
            
            var user = await membershipService.GetCurrentUser();

            if (args.Value is Media media)
            {
                var copyMediaResult = await mediaService.SaveMediaAsync(new SaveMediaParameters { MediaToSave = media, IsUpdate = true });
                if (!copyMediaResult.Success)
                {
                    notificationService.ShowNotifications(copyMediaResult.Messages, text);
                }
                else
                {
                    notificationService.ShowSuccessNotification(text["contextMenus_mediaMoved"]);
                    await appState.NotifyMediaChanged(media, user?.UserName ?? "Unknown");
                }
            }
            
        }
    }

    public int SortOrder => -98;
}