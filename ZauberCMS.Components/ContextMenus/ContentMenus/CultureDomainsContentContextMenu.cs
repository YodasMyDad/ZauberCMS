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
using ZauberCMS.Core.Shared;

namespace ZauberCMS.Components.ContextMenus.ContentMenus;

public class CultureDomainsContentContextMenu(
    IContentService contentService,
    IMembershipService membershipService,
    NotificationService notificationService,
    AppState appState) : ITreeContextMenu
{
    public List<string> Sections => [Constants.Sections.ContentSection];
    public List<string> TreeAlias { get; } = [];

    public string Text(TreeItemContextMenuEventArgs args)
    {
        var content = (Content)args.Value!;
        return content.IsRootContent ? "Culture & Domains" : "Culture";
    }

    public string Icon(TreeItemContextMenuEventArgs args) => "bungalow";

    public string IconColor(TreeItemContextMenuEventArgs args) => string.Empty;

    public bool CanShowContextMenu(TreeItemContextMenuEventArgs args)
    {
        if(args.Value is Content content)
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
        // The dialog edits and saves this fresh copy, not the tree's object: the tree's copy can be stale (a stale
        // DateUpdated is refused as a concurrent edit) and must not be changed if the dialog is cancelled.
        var dbContent = await contentService.GetContentAsync(new GetContentParameters
            { Id = content.Id, IncludeUnpublished = true, Cached = false });
        contextMenuService.Close();
        if (dbContent is null)
        {
            notificationService.ShowErrorNotification("Unable to load this content item");
            return;
        }

        var currentUser = await membershipService.GetCurrentUser();
        var languageDialog = modalService.OpenSidePanel<CultureDomains>("Domains & Culture",
            new Dictionary<string, object>
            {
                { "Content", dbContent }
            });
        var languageDialogResult = await languageDialog.Result;
        if (languageDialogResult is { Confirmed: true })
        {
            // The dialog has already saved the content and its domains; saving again here would be refused as a
            // concurrent edit (ZA-076).
            notificationService.ShowSuccessNotification("Culture Updated");
            await appState.NotifyContentChanged(languageDialogResult.Data as Content ?? dbContent, currentUser?.Name ?? "Unknown");
        }
    }

    public int SortOrder => -70;
}