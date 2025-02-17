using Blazored.Modal.Services;
using MediatR;
using Microsoft.AspNetCore.Components;
using Radzen;
using ZauberCMS.Components.Admin.Shared;
using ZauberCMS.Core;
using ZauberCMS.Core.Content.Commands;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Shared;

namespace ZauberCMS.Components.Trees.ContextMenus.ContentMenus;

public class SortContentContextMenu(IMediator mediator, NotificationService notificationService, AppState appState) : ITreeContextMenu
{
    public List<string> Sections => [Constants.Sections.ContentSection];
    public List<string> TreeAlias { get; } = [];
    public string Text(TreeItemContextMenuEventArgs args) => "Sort";
    public string Icon(TreeItemContextMenuEventArgs args) => string.Empty;
    public string IconColor(TreeItemContextMenuEventArgs args) => string.Empty;
    public bool CanShowContextMenu(TreeItemContextMenuEventArgs args)
    {
        return args.Value is Content;
    }

    public async Task ContextMenuAction(TreeItemContextMenuEventArgs args, MenuItemEventArgs e, NavigationManager navigationManager,
        ContextMenuService contextMenuService, IModalService modalService)
    {
        var content = (Content)args.Value!;
        var dbContent = await mediator.Send(new GetContentCommand { Id = content.Id, IncludeChildren = true });
        var currentUser = await mediator.GetCurrentUser();
        if (!dbContent!.Children.Any())
        {
            // Show message if no children
            notificationService.Notify(new NotificationMessage { Severity = NotificationSeverity.Warning, Summary = "Hmmmm", Detail = "Sorry, nothing to sort as this content has no children", Duration = 4000 });
        }
        else
        {
            contextMenuService.Close();
            var dialog = modalService.OpenSidePanel<SortContent>("Sort Children", 
                new Dictionary<string, object>{ { "ContentId", dbContent.Id }, {"Content", dbContent.Children.OrderBy(x => x.SortOrder).ToList()} });
            var result = await dialog.Result;
            if (result is { Confirmed: true, Data: List<Core.Content.Models.Content> sortedContent })
            {
                foreach (var c in sortedContent)
                {
                    var saveResult = await mediator.Send(new SaveContentCommand {Content = c, ExcludePropertyData = true});
                    if (!saveResult.Success)
                    {
                        notificationService.Notify(new NotificationMessage { Severity = NotificationSeverity.Error, Summary = "Error", Detail = saveResult.Messages.MessagesAsString(), Duration = 4000 });
                    }
                }
                await appState.NotifyContentChanged(dbContent, currentUser?.Name ?? "Unknown");
            }
        }

    }

    public int SortOrder => -80;
}