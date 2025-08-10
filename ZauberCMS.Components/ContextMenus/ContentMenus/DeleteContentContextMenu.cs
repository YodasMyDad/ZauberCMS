using Blazored.Modal.Services;
using Microsoft.AspNetCore.Components;
using Radzen;
using ZauberCMS.Core;
using ZauberCMS.Core.Content.Interfaces;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Content.Parameters;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Membership.Interfaces;
using ZauberCMS.Core.Shared;

namespace ZauberCMS.Components.ContextMenus.ContentMenus;

public class DeleteContentContextMenu(IContentService contentService, IMembershipService membershipService, DialogService confirmService, NotificationService notificationService, AppState appState) : ITreeContextMenu
{
    public List<string> Sections => [Constants.Sections.ContentSection];
    public List<string> TreeAlias { get; } = [];
    public string Text(TreeItemContextMenuEventArgs args) => "Delete";

    public string Icon(TreeItemContextMenuEventArgs args) => "close";

    public string IconColor(TreeItemContextMenuEventArgs args) => string.Empty;
    public bool CanShowContextMenu(TreeItemContextMenuEventArgs args)
    {
        return args.Value is Content;
    }

    public async Task ContextMenuAction(TreeItemContextMenuEventArgs args, MenuItemEventArgs e, NavigationManager navigationManager,
        ContextMenuService contextMenuService, IModalService modalService)
    {
        var content = (Content)args.Value!;
        var dbContent = await contentService.GetContentAsync(new GetContentParameters { Id = content.Id, IncludeChildren = true, IncludeUnpublished = true, Cached = false});
        var currentUser = await membershipService.GetCurrentUser();
        // Confirm dialogue, say if there are children, and confirm then delete all
        var hasChildren = dbContent!.Children.Count != 0;
        var message = hasChildren
            ? "Move this content and it's children to the recycle bin?"
            : "Move this content to the recycle bin?";
        var delete = await confirmService.Confirm(message, "Move to recycle bin", new ConfirmOptions { OkButtonText = "Yes", CancelButtonText = "No" });
        if (delete == true)
        {
            var result = await contentService.DeleteContentAsync(new DeleteContentParameters {ContentId = dbContent.Id, MoveToRecycleBin = true});
            notificationService.Notify(new NotificationMessage { 
                Severity = result.Success ? NotificationSeverity.Success : NotificationSeverity.Error, 
                Summary = result.Success ? "Success" : "Error", 
                Detail = result.Messages.MessagesAsString(), Duration = 4000 });
            if (result.Success)
            {
                await appState.NotifyContentChanged(dbContent, currentUser?.Name ?? "Unknown");
            }
        }
    }

    public int SortOrder => 150;
}