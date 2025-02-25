using Blazored.Modal;
using Blazored.Modal.Services;
using MediatR;
using Microsoft.AspNetCore.Components;
using Radzen;
using ZauberCMS.Components.Admin.Shared.Dialogs;
using ZauberCMS.Core;
using ZauberCMS.Core.Content.Commands;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Media.Commands;
using ZauberCMS.Core.Media.Models;
using ZauberCMS.Core.Shared;
using ZauberCMS.Core.Shared.Interfaces;

namespace ZauberCMS.Components.Trees.ContextMenus.Shared;

public class MoveItemContextMenu(NotificationService notificationService, IMediator mediator, AppState appState) : ITreeContextMenu
{
    public List<string> Sections => [Constants.Sections.ContentSection, Constants.Sections.MediaSection];
    public List<string> TreeAlias => [];
    public string Text(TreeItemContextMenuEventArgs args) => "Move";

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
            { nameof(MoveItem.Item), baseItem}
        };
        if (baseItem.ParentId != null)
        {
            parameters.Add(nameof(MoveItem.ParentId), baseItem.ParentId);
        }

        Modal = modalService.OpenSidePanel<MoveItem>(args.Value is Content ? "Move Content" : "Move Media", parameters);
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
            
            var user = await mediator.GetCurrentUser();
            
            if (args.Value is Content content)
            {
                var copyContentResult = await mediator.Send(new SaveContentCommand {Content = content, ExcludePropertyData = true});
                if (!copyContentResult.Success)
                {
                    notificationService.ShowNotifications(copyContentResult.Messages);
                }
                else
                {
                    notificationService.ShowSuccessNotification("Content Moved");
                    await appState.NotifyContentChanged(content, user?.UserName ?? "Unknown");
                }
            }

            if (args.Value is Media media)
            {
                var copyMediaResult = await mediator.Send(new SaveMediaCommand { MediaToSave = media, IsUpdate = true });
                if (!copyMediaResult.Success)
                {
                    notificationService.ShowNotifications(copyMediaResult.Messages);
                }
                else
                {
                    notificationService.ShowSuccessNotification("Media Moved");
                    await appState.NotifyMediaChanged(media, user?.UserName ?? "Unknown");
                }
            }
            
        }
    }

    public int SortOrder => -98;
}