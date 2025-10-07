using Blazored.Modal.Services;
using Microsoft.AspNetCore.Components;
using Radzen;
using ZauberCMS.Core;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Media.Interfaces;
using ZauberCMS.Core.Media.Models;
using ZauberCMS.Core.Media.Parameters;
using ZauberCMS.Core.Membership.Interfaces;
using ZauberCMS.Core.Shared;
using ZauberCMS.Core.Shared.Services;

namespace ZauberCMS.Components.ContextMenus.MediaMenus;

public class RefreshMediaContextMenu(
    IMembershipService membershipService,
    IMediaService mediaService,
    ICacheService cacheService,
    TreeState treeState,
    AppState appState) : ITreeContextMenu
{
    public List<string> Sections => [Constants.Sections.MediaSection];
    public List<string> TreeAlias { get; } = [];

    public string Text(TreeItemContextMenuEventArgs args) => "Refresh";

    public string Icon(TreeItemContextMenuEventArgs args) => "frame_reload";

    public string IconColor(TreeItemContextMenuEventArgs args) => string.Empty;

    public bool CanShowContextMenu(TreeItemContextMenuEventArgs args)
    {
        if(args.Value is Media media)
        {
            return media.MediaType == MediaType.Folder;
        }
        return false;
    }

    public async Task ContextMenuAction(TreeItemContextMenuEventArgs args, MenuItemEventArgs e,
        NavigationManager navigationManager,
        ContextMenuService contextMenuService, IModalService modalService)
    {
        var media = (Media)args.Value!;
        var dbMedia = await mediaService.GetMediaAsync(new GetMediaParameters { Id = media.Id, IncludeChildren = true });
        var currentUser = await membershipService.GetCurrentUser();
        contextMenuService.Close();
        cacheService.ClearCachedItemsWithPrefix(nameof(Core.Media));
        treeState.ClearChildCache(null);
        await appState.NotifyMediaChanged(dbMedia, currentUser?.Name ?? "Unknown");
    }

    public int SortOrder => 150;
}

