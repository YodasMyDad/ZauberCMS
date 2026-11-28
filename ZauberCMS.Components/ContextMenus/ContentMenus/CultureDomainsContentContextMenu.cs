using Blazored.Modal.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Radzen;
using ZauberCMS.Components.Admin.ContentSection.Dialogs;
using ZauberCMS.Core;
using ZauberCMS.Core.Content.Interfaces;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Content.Parameters;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Localization;
using ZauberCMS.Core.Membership.Interfaces;
using ZauberCMS.Core.Shared;

namespace ZauberCMS.Components.ContextMenus.ContentMenus;

public class CultureDomainsContentContextMenu(
    IContentService contentService,
    IMembershipService membershipService,
    NotificationService notificationService,
    AppState appState,
    IStringLocalizer<ZauberText> text) : ITreeContextMenu
{
    public List<string> Sections => [Constants.Sections.ContentSection];
    public List<string> TreeAlias { get; } = [];

    public string Text(TreeItemContextMenuEventArgs args)
    {
        var content = (Content)args.Value!;
        return content.IsRootContent ? text["contextMenus_cultureAndDomains"] : text["contextMenus_culture"];
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
            notificationService.ShowErrorNotification(text["contextMenus_unableToLoadContent"]);
            return;
        }

        var currentUser = await membershipService.GetCurrentUser();
        var languageDialog = modalService.OpenSidePanel<CultureDomains>(text["contextMenus_domainsAndCulture"],
            new Dictionary<string, object>
            {
                { "Content", dbContent }
            });
        var languageDialogResult = await languageDialog.Result;
        if (languageDialogResult is { Confirmed: true })
        {
            // The dialog has already saved the content and its domains; saving again here would be refused as a
            // concurrent edit (ZA-076).
            notificationService.ShowSuccessNotification(text["contextMenus_cultureUpdated"]);
            await appState.NotifyContentChanged(languageDialogResult.Data as Content ?? dbContent, currentUser?.Name ?? "Unknown");
        }
    }

    public int SortOrder => -70;
}