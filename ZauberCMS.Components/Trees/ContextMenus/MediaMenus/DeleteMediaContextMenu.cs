using Blazored.Modal.Services;
using MediatR;
using Microsoft.AspNetCore.Components;
using Radzen;
using ZauberCMS.Core;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Media.Commands;
using ZauberCMS.Core.Media.Models;

namespace ZauberCMS.Components.Trees.ContextMenus.MediaMenus;

public class DeleteMediaContextMenu(IMediator mediator, NotificationService notificationService) : ITreeContextMenu
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
        var deleteResult = await mediator.Send(new DeleteMediaCommand { MediaId = media.Id });
        if (deleteResult.Success)
        {
            // Only redirect if on item being deleted? How do we check that?
            //NavigationManager.NavigateTo("/admin/media", forceLoad: true);
            notificationService.ShowSuccessNotification("Media deleted");
        }
        else
        {
            notificationService.ShowErrorNotification(deleteResult.Messages.MessagesAsString());
        }
    }
    
    public int SortOrder => 100;
}