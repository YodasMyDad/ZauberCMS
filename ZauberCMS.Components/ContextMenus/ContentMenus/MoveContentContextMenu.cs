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
using ZauberCMS.Core.Media.Models;
using ZauberCMS.Core.Membership.Interfaces;
using ZauberCMS.Core.Shared;

namespace ZauberCMS.Components.ContextMenus.ContentMenus;

public class MoveContentContextMenu(NotificationService notificationService, IContentService contentService, IMembershipService membershipService, AppState appState) : ITreeContextMenu
{
    public List<string> Sections => [Constants.Sections.ContentSection];
    public List<string> TreeAlias => [];
    public string Text(TreeItemContextMenuEventArgs args) => "Move";

    public string Icon(TreeItemContextMenuEventArgs args) => "move_up";

    public string IconColor(TreeItemContextMenuEventArgs args) => string.Empty;

    public bool CanShowContextMenu(TreeItemContextMenuEventArgs args)
    {
        if(args.Value is Content content)
        {
            return content.Published;
        }

        return args.Value is Media;
    }

    private IModalReference? Modal { get; set; }
    
    public async Task ContextMenuAction(TreeItemContextMenuEventArgs args, MenuItemEventArgs e, NavigationManager navigationManager,
        ContextMenuService contextMenuService, IModalService modalService)
    {
        contextMenuService.Close();
        var baseItem = (Content)args.Value!;

        var parameters = new Dictionary<string, object>
        {
            { nameof(MoveContent.Item), baseItem }
        };
        if (baseItem.ParentId != null)
        {
            parameters.Add(nameof(MoveContent.ParentId), baseItem.ParentId);
        }

        Modal = modalService.OpenSidePanel<MoveContent>("Move Content", parameters);
        var result = await Modal.Result;
        if (result is { Confirmed: true, Data: Guid parentId })
        {
            if (parentId == Guid.Empty)
            {
                // If this is null, they are trying to put it in the root
                // get the content type and see if it's allowed in the root
                var contentType = await contentService.GetContentTypeAsync(new GetContentTypeParameters { Id = baseItem.ContentTypeId });
                if (contentType?.AllowAtRoot == true)
                {
                    baseItem.ParentId = null;
                    baseItem.IsRootContent = true;
                }
                else
                {
                    notificationService.ShowNotification("Content Type does not allow this to be moved to the root", NotificationSeverity.Warning);
                    return;
                }
            }
            else
            {
                baseItem.ParentId = parentId;
                
                // Can't be a root item if it has a parent id 
                baseItem.IsRootContent = false;
            }
            
            var user = await membershipService.GetCurrentUser();
            
            if (args.Value is Content content)
            {
                var copyContentResult = await contentService.SaveContentAsync(new SaveContentParameters {Content = content, ExcludePropertyData = true});
                if (!copyContentResult.Success)
                {
                    notificationService.ShowNotifications(copyContentResult.Messages);
                }
                else
                {
                    notificationService.ShowSuccessNotification("Content Moved");
                    await appState.NotifyContentChanged(content, user?.UserName ?? "Unknown");
                }
            }
            
        }
    }

    public int SortOrder => -98;
}