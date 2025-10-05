using Blazored.Modal.Services;
using Microsoft.AspNetCore.Components;
using Radzen;
using ZauberCMS.Core;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Media.Interfaces;
using ZauberCMS.Core.Media.Models;
using ZauberCMS.Core.Media.Parameters;
using ZauberCMS.Core.Membership.Interfaces;
using ZauberCMS.Core.Shared;

namespace ZauberCMS.Components.ContextMenus.MediaMenus;

public class DeleteMediaContextMenu(
    IMediaService mediaService, 
    IMembershipService membershipService,
    DialogService dialogService,
    NotificationService notificationService,
    AppState appState) : ITreeContextMenu
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
        var dbMedia = await mediaService.GetMediaAsync(new GetMediaParameters { Id = media.Id, IncludeChildren = true, Cached = false });
        var currentUser = await membershipService.GetCurrentUser();
        
        // Show confirmation dialog
        var hasChildren = dbMedia?.Children.Count > 0;
        var message = hasChildren
            ? "Are you sure you want to delete this media and all its children?"
            : "Are you sure you want to delete this media?";
        var delete = await dialogService.Confirm(message, "Delete Media", new ConfirmOptions { OkButtonText = "Yes", CancelButtonText = "No" });
        
        if (delete == true && dbMedia != null)
        {
            var deleteResult = await mediaService.DeleteMediaAsync(new DeleteMediaParameters { MediaId = dbMedia.Id });
            if (deleteResult.Success)
            {
                notificationService.ShowSuccessNotification("Media deleted");
                await appState.NotifyMediaDeleted(dbMedia, currentUser?.Name ?? "Unknown");
            }
            else
            {
                notificationService.ShowErrorNotification(deleteResult.Messages.MessagesAsString());
            }
        }
    }
    
    public int SortOrder => 100;
}