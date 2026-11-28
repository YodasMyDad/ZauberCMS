using Blazored.Modal.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Radzen;
using ZauberCMS.Core;
using ZauberCMS.Core.Content.Interfaces;
using ZauberCMS.Core.Content.Parameters;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Localization;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Components.ContextMenus.RecycleBinMenus;

public class FinalDeleteContentContextMenu(IContentService contentService, DialogService dialogService, NotificationService notificationService, IStringLocalizer<ZauberText> text) : ITreeContextMenu
{
    public List<string> Sections { get; } = [];
    public List<string> TreeAlias { get; } = [Constants.Sections.Trees.RecycleBinTree];
    public string Text(TreeItemContextMenuEventArgs args) => text["general_delete"];

    public string Icon(TreeItemContextMenuEventArgs args) => "dangerous";

    public string IconColor(TreeItemContextMenuEventArgs args) => string.Empty;

    public bool CanShowContextMenu(TreeItemContextMenuEventArgs args)
    {
        return args.Value is TreeBranch;
    }

    public async Task ContextMenuAction(TreeItemContextMenuEventArgs args, MenuItemEventArgs e, NavigationManager navigationManager,
        ContextMenuService contextMenuService, IModalService modalService)
    {
        if (args.Value is not TreeBranch branch)
        {
            return;
        }

        var dbContent = await contentService.GetContentAsync(new GetContentParameters { Id = branch.Id, IncludeChildren = true, IncludeUnpublished = true, IncludeDeleted = true });
        if (dbContent is null)
        {
            return;
        }

        contextMenuService.Close();
        var delete = await dialogService.Confirm(text["contextMenus_deletePermanentlyConfirm"], text["general_delete"], new ConfirmOptions { OkButtonText = text["general_yes"], CancelButtonText = text["general_no"] });
        if (delete == true)
        {
            var result = await contentService.DeleteContentAsync(new DeleteContentParameters { ContentId = dbContent.Id });
            notificationService.Notify(new NotificationMessage { 
                Severity = result.Success ? NotificationSeverity.Success : NotificationSeverity.Error, 
                Summary = result.Success ? text["general_success"] : text["general_error"], 
                Detail = result.Messages.MessagesAsString(), Duration = 4000 });
            if (result.Success)
            {
                // Hard reload as could be in the edit screen
                navigationManager.NavigateTo(navigationManager.ToSiteUrl(Urls.AdminBaseUrl), forceLoad: true); 
            }
        }
    }

    public int SortOrder => -90;
}