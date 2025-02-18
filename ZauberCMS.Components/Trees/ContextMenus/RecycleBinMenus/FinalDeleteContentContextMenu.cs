using Blazored.Modal.Services;
using MediatR;
using Microsoft.AspNetCore.Components;
using Radzen;
using ZauberCMS.Core;
using ZauberCMS.Core.Content.Commands;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Components.Trees.ContextMenus.RecycleBinMenus;

public class FinalDeleteContentContextMenu(IMediator mediator, DialogService dialogService, NotificationService notificationService) : ITreeContextMenu
{
    public List<string> Sections { get; } = [];
    public List<string> TreeAlias { get; } = [Constants.Sections.Trees.RecycleBinTree];
    public string Text(TreeItemContextMenuEventArgs args) => "Delete";

    public string Icon(TreeItemContextMenuEventArgs args) => "dangerous";

    public string IconColor(TreeItemContextMenuEventArgs args) => string.Empty;

    public bool CanShowContextMenu(TreeItemContextMenuEventArgs args)
    {
        return args.Value is TreeBranch;
    }

    public async Task ContextMenuAction(TreeItemContextMenuEventArgs args, MenuItemEventArgs e, NavigationManager navigationManager,
        ContextMenuService contextMenuService, IModalService modalService)
    {
        var branch = (TreeBranch)args.Value;
        var dbContent = await mediator.Send(new GetContentCommand { Id = branch.Id, IncludeChildren = true });
        contextMenuService.Close();
        var delete = await dialogService.Confirm("Permanently delete this?", "Delete", new ConfirmOptions { OkButtonText = "Yes", CancelButtonText = "No" });
        if (delete == true)
        {
            var result = await mediator.Send(new DeleteContentCommand{ContentId = dbContent!.Id});
            notificationService.Notify(new NotificationMessage { 
                Severity = result.Success ? NotificationSeverity.Success : NotificationSeverity.Error, 
                Summary = result.Success ? "Success" : "Error", 
                Detail = result.Messages.MessagesAsString(), Duration = 4000 });
            if (result.Success)
            {
                // Hard reload as could be in the edit screen
                navigationManager.NavigateTo("/admin", forceLoad: true); 
            }
        }
    }

    public int SortOrder => -90;
}