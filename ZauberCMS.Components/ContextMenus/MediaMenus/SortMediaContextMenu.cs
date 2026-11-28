using Blazored.Modal.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Radzen;
using ZauberCMS.Components.Admin.Shared;
using ZauberCMS.Core;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Localization;
using ZauberCMS.Core.Media.Interfaces;
using ZauberCMS.Core.Media.Models;
using ZauberCMS.Core.Media.Parameters;
using ZauberCMS.Core.Membership.Interfaces;
using ZauberCMS.Core.Shared;

namespace ZauberCMS.Components.ContextMenus.MediaMenus;

public class SortMediaContextMenu(IMembershipService membershipService, IMediaService mediaService, NotificationService notificationService, AppState appState, IStringLocalizer<ZauberText> text)
    : ITreeContextMenu
{
    public List<string> Sections => [Constants.Sections.MediaSection];
    public List<string> TreeAlias { get; } = [];
    public string Text(TreeItemContextMenuEventArgs args) => text["general_sort"];
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
        var dbContent = await mediaService.GetMediaAsync(new GetMediaParameters { Id = content.Id, IncludeChildren = true });
        var currentUser = await membershipService.GetCurrentUser();
        if (dbContent!.Children.Count == 0)
        {
            // Show message if no children
            notificationService.Notify(new NotificationMessage
            {
                Severity = NotificationSeverity.Warning, Summary = text["contextMenus_nothingToSort"],
                Detail = text["contextMenus_folderHasNoChildrenToSort"], Duration = 4000
            });
        }
        else
        {
            contextMenuService.Close();
            var dialog = modalService.OpenSidePanel<SortChilden<Media>>(text["contextMenus_sortChildren"],
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
                    var saveResult = await mediaService.SaveMediaAsync(new SaveMediaParameters { MediaToSave = c, IsUpdate = true });
                    if (!saveResult.Success)
                    {
                        notificationService.ShowNotifications(saveResult.Messages, text);
                    }
                }

                await appState.NotifyMediaChanged(dbContent, currentUser?.Name ?? "Unknown");
            }
        }
    }

    public int SortOrder => -80;
}