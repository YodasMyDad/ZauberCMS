using Blazored.Modal.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Radzen;
using ZauberCMS.Components.Admin.Shared;
using ZauberCMS.Core;
using ZauberCMS.Core.Content.Interfaces;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Content.Parameters;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Localization;
using ZauberCMS.Core.Membership.Interfaces;
using ZauberCMS.Core.Shared;

namespace ZauberCMS.Components.ContextMenus.ContentMenus;

public class SortContentContextMenu(IContentService contentService, IMembershipService membershipService, NotificationService notificationService, AppState appState, IStringLocalizer<ZauberText> text)
    : ITreeContextMenu
{
    public List<string> Sections => [Constants.Sections.ContentSection];
    public List<string> TreeAlias { get; } = [];
    public string Text(TreeItemContextMenuEventArgs args) => text["general_sort"];
    public string Icon(TreeItemContextMenuEventArgs args) => "swap_vert";
    public string IconColor(TreeItemContextMenuEventArgs args) => string.Empty;

    public bool CanShowContextMenu(TreeItemContextMenuEventArgs args)
    {
        if (args.Value is Content content)
        {
            return content.Published;
        }

        return false;
    }

    public async Task ContextMenuAction(TreeItemContextMenuEventArgs args, MenuItemEventArgs e,
        NavigationManager navigationManager,
        ContextMenuService contextMenuService, IModalService modalService)
    {
        var content = (Content)args.Value!;
        var dbContent = await contentService.GetContentAsync(new GetContentParameters { Id = content.Id, IncludeChildren = true });
        var currentUser = await membershipService.GetCurrentUser();
        if (dbContent!.Children.Count == 0)
        {
            // Show message if no children
            notificationService.Notify(new NotificationMessage
            {
                Severity = NotificationSeverity.Warning, Summary = text["contextMenus_nothingToSort"],
                Detail = text["contextMenus_contentHasNoChildrenToSort"], Duration = 4000
            });
        }
        else
        {
            contextMenuService.Close();
            var dialog = modalService.OpenSidePanel<SortChilden<Content>>(text["contextMenus_sortChildren"],
                new Dictionary<string, object>
                {
                    { nameof(SortChilden<Content>.ItemId), dbContent.Id },
                    { nameof(SortChilden<Content>.Items), dbContent.Children.OrderBy(x => x.SortOrder).ToList() }
                });
            var result = await dialog.Result;
            if (result is { Confirmed: true, Data: List<Content> sortedContent })
            {
                foreach (var c in sortedContent)
                {
                    var saveResult = await contentService.SaveContentAsync(new SaveContentParameters
                        { Content = c, ExcludePropertyData = true });
                    if (!saveResult.Success)
                    {
                        notificationService.Notify(new NotificationMessage
                        {
                            Severity = NotificationSeverity.Error, Summary = text["general_error"],
                            Detail = saveResult.Messages.MessagesAsString(), Duration = 4000
                        });
                    }
                }

                await appState.NotifyContentChanged(dbContent, currentUser?.Name ?? "Unknown");
            }
        }
    }

    public int SortOrder => -80;
}