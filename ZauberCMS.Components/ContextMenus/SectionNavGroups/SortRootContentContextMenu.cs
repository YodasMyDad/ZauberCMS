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
using ZauberCMS.Core.Sections.Interfaces;
using ZauberCMS.Core.Shared;

namespace ZauberCMS.Components.ContextMenus.SectionNavGroups;

public class SortRootContentContextMenu(IContentService contentService, IMembershipService membershipService, NotificationService notificationService, AppState appState, IStringLocalizer<ZauberText> text) : ISectionNavGroupAction
{
    public string Text => text["contextMenus_sortContent"];
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
                Severity = NotificationSeverity.Warning, Summary = text["contextMenus_sorry"],
                Detail = text["contextMenus_onlyOneRootContent"], Duration = 4000
            });
        }
        else
        {
            contextMenuService.Close();
            var dialog = modalService.OpenSidePanel<SortChilden<Content>>(text["contextMenus_sortRootItems"],
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
                            Severity = NotificationSeverity.Error, Summary = text["general_error"],
                            Detail = saveResult.Messages.MessagesAsString(), Duration = 4000
                        });
                    }
                }

                await appState.NotifyContentChanged(null, currentUser?.Name ?? "Unknown");
            }
        }
    }
}