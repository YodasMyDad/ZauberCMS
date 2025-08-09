using Blazored.Modal.Services;
using Microsoft.AspNetCore.Components;
using Radzen;
using ZauberCMS.Components.Admin.Shared;
using ZauberCMS.Core;
using ZauberCMS.Core.Content.Interfaces;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Content.Parameters;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Membership.Interfaces;
using ZauberCMS.Core.Sections.Interfaces;
using ZauberCMS.Core.Shared;

namespace ZauberCMS.Components.ContextMenus.SectionNavGroups;

public class SortRootContentContextMenu(IContentService contentService, IMembershipService membershipService, NotificationService notificationService, AppState appState) : ISectionNavGroupAction
{
    public string Text => "Sort Content";
    public string Icon => "swap_vert";
    public string IconColor => string.Empty;
    public string SectionNavGroupAlias => Constants.Sections.SectionNavGroups.ContentNavGroup;
    public int SortOrder => -100;

    public async Task ContextMenuAction(MenuItemEventArgs e, NavigationManager navigationManager, ContextMenuService contextMenuService,
        IModalService modalService)
    {
        contextMenuService.Close();
        
        // If there is only one root, show message
        
        // Open the sort dialog
        var rootItems = await contentService.QueryContentAsync(new QueryContentParameters { RootContentOnly = true, AmountPerPage = 100 });
        var currentUser = await membershipService.GetCurrentUser();
        if (rootItems.Items.Count() <= 1)
        {
            // Show message if no children
            notificationService.Notify(new NotificationMessage
            {
                Severity = NotificationSeverity.Warning, Summary = "Sorry",
                Detail = "Unable to sort as there only one root content", Duration = 4000
            });
        }
        else
        {
            contextMenuService.Close();
            var dialog = modalService.OpenSidePanel<SortChilden<Content>>("Sort Root Items",
                new Dictionary<string, object>
                {
                    { nameof(SortChilden<Content>.Items), rootItems.Items.OrderBy(x => x.SortOrder).ToList() }
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
                            Severity = NotificationSeverity.Error, Summary = "Error",
                            Detail = saveResult.Messages.MessagesAsString(), Duration = 4000
                        });
                    }
                }

                await appState.NotifyContentChanged(null, currentUser?.Name ?? "Unknown");
            }
        }
    }
}