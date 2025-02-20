using Blazored.Modal;
using Blazored.Modal.Services;
using MediatR;
using Microsoft.AspNetCore.Components;
using Radzen;
using ZauberCMS.Components.Admin.ContentSection.Dialogs;
using ZauberCMS.Core;
using ZauberCMS.Core.Content.Commands;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Shared;

namespace ZauberCMS.Components.Trees.ContextMenus.ContentMenus;

public class MoveContentContextMenu(NotificationService notificationService, IMediator mediator, AppState appState) : ITreeContextMenu
{
    public List<string> Sections => [Constants.Sections.ContentSection];
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
        return false;
    }

    private IModalReference? Modal { get; set; }
    
    public async Task ContextMenuAction(TreeItemContextMenuEventArgs args, MenuItemEventArgs e, NavigationManager navigationManager,
        ContextMenuService contextMenuService, IModalService modalService)
    {
        contextMenuService.Close();
        var content = (Content)args.Value!;

        var parameters = new Dictionary<string, object>
        {
            { nameof(MoveContent.Content), content}
        };
        if (content.ParentId != null)
        {
            parameters.Add(nameof(MoveContent.ParentId), content.ParentId);
        }

        Modal = modalService.OpenSidePanel<MoveContent>("Move Content", parameters);
        var result = await Modal.Result;
        if (result is { Confirmed: true, Data: Guid parentId })
        {
            if (parentId == Guid.Empty)
            {
                content.ParentId = null;
            }
            else
            {
                content.ParentId = parentId;
            }
            var copyContentResult = await mediator.Send(new SaveContentCommand {Content = content, ExcludePropertyData = true});
            if (!copyContentResult.Success)
            {
                notificationService.ShowNotifications(copyContentResult.Messages);
            }
            else
            {
                notificationService.ShowSuccessNotification("Content Moved");
                var user = await mediator.GetCurrentUser();
                await appState.NotifyContentChanged(content, user?.UserName ?? "Unknown");
            }
        }
    }

    public int SortOrder => -98;
}