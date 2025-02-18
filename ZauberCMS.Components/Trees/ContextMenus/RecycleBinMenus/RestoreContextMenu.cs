using Blazored.Modal.Services;
using MediatR;
using Microsoft.AspNetCore.Components;
using Radzen;
using ZauberCMS.Core;
using ZauberCMS.Core.Content.Commands;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Shared;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Components.Trees.ContextMenus.RecycleBinMenus;

public class RestoreContextMenu(IMediator mediator, AppState appState) : ITreeContextMenu
{
    public List<string> Sections { get; } = [];
    public List<string> TreeAlias { get; } = [Constants.Sections.Trees.RecycleBinTree];
    public string Text(TreeItemContextMenuEventArgs args) => "Restore";

    public string Icon(TreeItemContextMenuEventArgs args) => "settings_backup_restore";

    public string IconColor(TreeItemContextMenuEventArgs args) => string.Empty;

    public bool CanShowContextMenu(TreeItemContextMenuEventArgs args)
    {
        return args.Value is TreeBranch;
    }

    public async Task ContextMenuAction(TreeItemContextMenuEventArgs args, MenuItemEventArgs e, NavigationManager navigationManager,
        ContextMenuService contextMenuService, IModalService modalService)
    {
        var branch = (TreeBranch)args.Value;
        var dbContent = await mediator.Send(new GetContentCommand { Id = branch.Id, IncludeChildren = true });
        var currentUser = await mediator.GetCurrentUser();
        contextMenuService.Close();
        dbContent!.Deleted = false;
        var saveResult = await mediator.Send(new SaveContentCommand{ Content = dbContent, ExcludePropertyData = true});
        if (saveResult.Success)
        {
            await appState.NotifyContentChanged(dbContent, currentUser?.Name ?? "Unknown");
        }
    }

    public int SortOrder => -100;
}