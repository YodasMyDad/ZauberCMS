using Blazored.Modal;
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

public class CopyContentContextMenu(NotificationService notificationService, IContentService contentService, IMembershipService membershipService, AppState appState, IStringLocalizer<ZauberText> text) : ITreeContextMenu
{
    public List<string> Sections => [Constants.Sections.ContentSection];
    public List<string> TreeAlias => [];
    public string Text(TreeItemContextMenuEventArgs args) => text["general_copy"];

    public string Icon(TreeItemContextMenuEventArgs args) => "content_copy";

    public string IconColor(TreeItemContextMenuEventArgs args) => string.Empty;

    public bool CanShowContextMenu(TreeItemContextMenuEventArgs args)
    {
        if(args.Value is Content content)
        {
            return content.Published;
        }
        return false;
    }

    private IModalReference? Modal { get; set; }
    private CopyContentParameters CopyContentCommand { get; set; } = new();
    
    public async Task ContextMenuAction(TreeItemContextMenuEventArgs args, MenuItemEventArgs e, NavigationManager navigationManager,
        ContextMenuService contextMenuService, IModalService modalService)
    {
        contextMenuService.Close();
        var content = (Content)args.Value!;
        CopyContentCommand.ContentToCopy = content.Id;
        var parameters = new Dictionary<string, object>
        {
            { nameof(CopyContent.Content), content},
            { nameof(CopyContent.CopyContentCommand), CopyContentCommand}
        };

        Modal = modalService.OpenSidePanel<CopyContent>(text["contextMenus_copyContent"], parameters);
        var result = await Modal.Result;
        if (result is { Confirmed: true, Data: CopyContentParameters copyContentModel })
        {
            var copyContentResult = await contentService.CopyContentAsync(copyContentModel);
            if (!copyContentResult.Success)
            {
                notificationService.ShowNotifications(copyContentResult.Messages, text);
            }
            else
            {
                notificationService.ShowSuccessNotification(text["contextMenus_contentCopied"]);
                var user = await membershipService.GetCurrentUser();
                await appState.NotifyContentChanged(content, user?.UserName ?? "Unknown");
            }
        }
    }

    public int SortOrder => -99;
}