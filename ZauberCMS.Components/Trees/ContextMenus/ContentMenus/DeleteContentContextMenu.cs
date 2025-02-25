using Blazored.Modal.Services;
using MediatR;
using Microsoft.AspNetCore.Components;
using Radzen;
using ZauberCMS.Core;
using ZauberCMS.Core.Content.Commands;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Shared;

namespace ZauberCMS.Components.Trees.ContextMenus.ContentMenus;

public class DeleteContentContextMenu(IMediator mediator, DialogService confirmService, NotificationService notificationService, AppState appState) : ITreeContextMenu
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
        var dbContent = await mediator.Send(new GetContentCommand { Id = content.Id, IncludeChildren = true, IncludeUnpublished = true, Cached = false});
        var currentUser = await mediator.GetCurrentUser();
        // Confirm dialogue, say if there are children, and confirm then delete all
        var hasChildren = dbContent!.Children.Count != 0;
        var message = hasChildren
            ? "Move this content and it's children to the recycle bin?"
            : "Move this content to the recycle bin?";
        var delete = await confirmService.Confirm(message, "Move to recycle bin", new ConfirmOptions { OkButtonText = "Yes", CancelButtonText = "No" });
        if (delete == true)
        {
            var result = await mediator.Send(new DeleteContentCommand{ContentId = dbContent.Id, MoveToRecycleBin = true});
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

    public int SortOrder => 100;
}