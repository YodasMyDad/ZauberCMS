using Blazored.Modal.Services;
using Microsoft.AspNetCore.Components;
using Radzen;
using ZauberCMS.Components.Admin.Shared.Dialogs;
using ZauberCMS.Core;
using ZauberCMS.Core.Content.Commands;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Media.Commands;
using ZauberCMS.Core.Media.Models;

namespace ZauberCMS.Components.ContextMenus.Structure;

public class MoveContentTypeContextMenu : ITreeContextMenu
{
    public List<string> Sections { get; } = [];
    public List<string> TreeAlias { get; } = [Constants.Sections.Trees.StructureContentTypeTree, Constants.Sections.Trees.StructureElementTypeTree];
    public string Text(TreeItemContextMenuEventArgs args) => "Move";

    public string Icon(TreeItemContextMenuEventArgs args) => "move_up";

    public string IconColor(TreeItemContextMenuEventArgs args) => string.Empty;

    public bool CanShowContextMenu(TreeItemContextMenuEventArgs args)
    {
        return args.Value is ContentType;
    }

    public Task ContextMenuAction(TreeItemContextMenuEventArgs args, MenuItemEventArgs e, NavigationManager navigationManager,
        ContextMenuService contextMenuService, IModalService modalService)
    {
        contextMenuService.Close();
        var baseItem = (ContentType)args.Value!;

        /*var parameters = new Dictionary<string, object>
        {
            { nameof(MoveItem.Item), baseItem}
        };
        if (baseItem.ParentId != null)
        {
            parameters.Add(nameof(MoveItem.ParentId), baseItem.ParentId);
        }

        Modal = modalService.OpenSidePanel<MoveItem>(args.Value is Content ? "Move Content" : "Move Media", parameters);
        var result = await Modal.Result;*/

        
        
        return Task.CompletedTask;
    }

    public int SortOrder => -98;
}