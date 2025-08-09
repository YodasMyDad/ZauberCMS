using Blazored.Modal.Services;
using Microsoft.AspNetCore.Components;
using Radzen;
using ZauberCMS.Core;
using ZauberCMS.Core.Content.Interfaces;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Content.Parameters;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Membership.Interfaces;
using ZauberCMS.Core.Shared;
using ZauberCMS.Core.Shared.Services;

namespace ZauberCMS.Components.ContextMenus.ContentMenus;

public class RefreshContentContextMenu(
    IMembershipService membershipService,
    IContentService contentService,
    ICacheService cacheService,
    TreeState treeState,
    AppState appState) : ITreeContextMenu
{
    public List<string> Sections => [Constants.Sections.ContentSection];
    public List<string> TreeAlias { get; } = [];

    public string Text(TreeItemContextMenuEventArgs args) => "Refresh";

    public string Icon(TreeItemContextMenuEventArgs args) => "frame_reload";

    public string IconColor(TreeItemContextMenuEventArgs args) => string.Empty;

    public bool CanShowContextMenu(TreeItemContextMenuEventArgs args)
    {
        if(args.Value is Content content)
        {
            return content.IsRootContent;
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
        contextMenuService.Close();
        cacheService.ClearCachedItemsWithPrefix(nameof(Core.Content));
        treeState.ClearChildCache(null);
        await appState.NotifyContentChanged(dbContent, currentUser?.Name ?? "Unknown");
    }

    public int SortOrder => -50;
}