using Blazored.Modal.Services;
using MediatR;
using Microsoft.AspNetCore.Components;
using Radzen;
using ZauberCMS.Components.Admin.Shared;
using ZauberCMS.Core;
using ZauberCMS.Core.Content.Interfaces;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Media.Commands;
using ZauberCMS.Core.Media.Models;
using ZauberCMS.Core.Shared;

namespace ZauberCMS.Components.Trees.ContextMenus.MediaMenus;

public class SortMediaContextMenu(IMediator mediator, NotificationService notificationService, AppState appState)
    : ITreeContextMenu
{
    public List<string> Sections => [Constants.Sections.MediaSection];
    public List<string> TreeAlias { get; } = [];
    public string Text(TreeItemContextMenuEventArgs args) => "Sort";
    public string Icon(TreeItemContextMenuEventArgs args) => "swap_vert";
    public string IconColor(TreeItemContextMenuEventArgs args) => string.Empty;

    public bool CanShowContextMenu(TreeItemContextMenuEventArgs args)
    {
        if (args.Value is Media media)
        {
            return media.MediaType == MediaType.Folder;
        }

        return false;
    }

    public async Task ContextMenuAction(TreeItemContextMenuEventArgs args, MenuItemEventArgs e,
        NavigationManager navigationManager,
        ContextMenuService contextMenuService, IModalService modalService)
    {
        var content = (Media)args.Value!;
        var dbContent = await mediator.Send(new GetMediaCommand { Id = content.Id, IncludeChildren = true });
        var currentUser = await mediator.GetCurrentUser();
        if (!dbContent!.Children.Any())
        {
            // Show message if no children
            notificationService.Notify(new NotificationMessage
            {
                Severity = NotificationSeverity.Warning, Summary = "Hmmmm",
                Detail = "Sorry, nothing to sort as this folder has no children", Duration = 4000
            });
        }
        else
        {
            contextMenuService.Close();
            var dialog = modalService.OpenSidePanel<SortChilden<Media>>("Sort Children",
                new Dictionary<string, object>
                {
                    { nameof(SortChilden<Media>.ItemId), dbContent.Id },
                    { nameof(SortChilden<Media>.Items), dbContent.Children.OrderBy(x => x.SortOrder).ToList() }
                });
            var result = await dialog.Result;
            if (result is { Confirmed: true, Data: List<Media> sortedMedia })
            {
                foreach (var c in sortedMedia)
                {
                    var saveResult = await mediator.Send(new SaveMediaCommand { MediaToSave = c, IsUpdate = true });
                    if (!saveResult.Success)
                    {
                        notificationService.ShowNotifications(saveResult.Messages);
                    }
                }

                await appState.NotifyMediaChanged(dbContent, currentUser?.Name ?? "Unknown");
            }
        }
    }

    public int SortOrder => -80;
}