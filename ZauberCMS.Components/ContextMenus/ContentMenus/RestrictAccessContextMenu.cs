using Blazored.Modal;
using Blazored.Modal.Services;
using Microsoft.AspNetCore.Components;
using Radzen;
using ZauberCMS.Components.Admin.ContentSection.Dialogs;
using ZauberCMS.Core;
using ZauberCMS.Core.Content.Interfaces;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Content.Parameters;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Membership.Interfaces;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Shared;

namespace ZauberCMS.Components.ContextMenus.ContentMenus;

public class RestrictAccessContextMenu(
    NotificationService notificationService,
    IMembershipService membershipService,
    IContentService contentService,
    AppState appState)
    : ITreeContextMenu
{
    public List<string> Sections => [Constants.Sections.ContentSection];
    public List<string> TreeAlias => [];
    public string Text(TreeItemContextMenuEventArgs args) => "Public Access";

    public string Icon(TreeItemContextMenuEventArgs args) => "lock";

    public string IconColor(TreeItemContextMenuEventArgs args) => string.Empty;

    public bool CanShowContextMenu(TreeItemContextMenuEventArgs args)
    {
        if (args.Value is Content content)
        {
            return content.Published && !content.ViewComponent.IsNullOrWhiteSpace();
        }

        return false;
    }

    private IModalReference? Modal { get; set; }

    public async Task ContextMenuAction(TreeItemContextMenuEventArgs args, MenuItemEventArgs e,
        NavigationManager navigationManager,
        ContextMenuService contextMenuService, IModalService modalService)
    {
        var currentUser = await membershipService.GetCurrentUser();
        contextMenuService.Close();
        var content = (Content)args.Value!;
        var parameters = new Dictionary<string, object>
        {
            { nameof(RestrictAccess.ContentId), content.Id }
        };

        Modal = modalService.OpenSidePanel<RestrictAccess>("Public Access", parameters);
        var result = await Modal.Result;
        if (result is { Confirmed: true, Data: (bool _, List<Role> selectedRoles) })
        {
            // The page and every page below it in one change, checked against the ancestors by the service (ZA-073)
            var saveResult = await contentService.SetContentRolesAsync(new SetContentRolesParameters
            {
                ContentId = content.Id,
                RoleIds = selectedRoles.Select(r => r.Id).ToList()
            });
            if (!saveResult.Success)
            {
                notificationService.ShowNotifications(saveResult.Messages);
                return;
            }

            notificationService.ShowSuccessNotification(selectedRoles.Count == 0
                ? "Public access removed"
                : "Public access updated");

            await appState.NotifyContentChanged(content, currentUser?.Name ?? "Unknown");
        }
    }

    public int SortOrder => 50;
}