using Blazored.Modal;
using Blazored.Modal.Services;
using Microsoft.AspNetCore.Components;
using Radzen;
using ZauberCMS.Components.Admin.StructureSection.Dialogs;
using ZauberCMS.Core;
using ZauberCMS.Core.Content.Interfaces;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Content.Parameters;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Membership.Interfaces;
using ZauberCMS.Core.Shared;

namespace ZauberCMS.Components.ContextMenus.Structure;

public class MoveContentTypeContextMenu(NotificationService notificationService, IContentService contentService, IMembershipService membershipService, AppState appState)
    : ITreeContextMenu
{
    public List<string> Sections { get; } = [];

    public List<string> TreeAlias { get; } =
        [Constants.Sections.Trees.StructureContentTypeTree, Constants.Sections.Trees.StructureElementTypeTree, Constants.Sections.Trees.StructureCompositionsTree];

    public string Text(TreeItemContextMenuEventArgs args) => "Move";

    public string Icon(TreeItemContextMenuEventArgs args) => "move_up";

    public string IconColor(TreeItemContextMenuEventArgs args) => string.Empty;

    public bool CanShowContextMenu(TreeItemContextMenuEventArgs args)
    {
        return args.Value is ContentType;
    }

    private IModalReference? Modal { get; set; }

    public async Task ContextMenuAction(TreeItemContextMenuEventArgs args, MenuItemEventArgs e,
        NavigationManager navigationManager,
        ContextMenuService contextMenuService, 
        IModalService modalService)
    {
        contextMenuService.Close();
        var baseItem = (ContentType)args.Value!;
        
        var parameters = new Dictionary<string, object>
        {
            { nameof(MoveContentType.Item), baseItem },
            { nameof(MoveContentType.IsElementType), baseItem.IsElementType },
            { nameof(MoveContentType.IsComposition), baseItem.IsComposition }
        };

        Modal = modalService.OpenSidePanel<MoveContentType>("Move Content Type", parameters);
        var result = await Modal.Result;

        if (result is { Confirmed: true, Data: Guid parentId })
        {
            if (parentId == Guid.Empty)
            {
                baseItem.ParentId = null;
            }
            else
            {
                baseItem.ParentId = parentId;
            }

            var user = await membershipService.GetCurrentUser();

            var copyContentTypeResult = await contentService.SaveContentTypeAsync(new SaveContentTypeParameters
                { ContentType = baseItem });
            if (!copyContentTypeResult.Success)
            {
                notificationService.ShowNotifications(copyContentTypeResult.Messages);
            }
            else
            {
                notificationService.ShowSuccessNotification("Content Type Moved");
                await appState.NotifyContentTypeChanged(baseItem, user?.UserName ?? "Unknown");
            }
        }
    }

    public int SortOrder => -98;
}