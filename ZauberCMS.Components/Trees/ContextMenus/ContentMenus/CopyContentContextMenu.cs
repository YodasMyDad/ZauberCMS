using Blazored.Modal;
using Blazored.Modal.Services;
using MediatR;
using Microsoft.AspNetCore.Components;
using Radzen;
using ZauberCMS.Components.Admin.ContentSection.Dialogs;
using ZauberCMS.Core;
using ZauberCMS.Core.Content.Commands;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Shared;

namespace ZauberCMS.Components.Trees.ContextMenus.ContentMenus;

public class CopyContentContextMenu(NotificationService notificationService, IMediator mediator, AppState appState) : ITreeContextMenu
{
    public List<string> Sections => [Constants.Sections.ContentSection];
    public List<string> TreeAlias => [];
    public string Text(TreeItemContextMenuEventArgs args) => "Copy";

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
    private CopyContentCommand CopyContentCommand { get; set; } = new();
    
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

        Modal = modalService.OpenSidePanel<CopyContent>("Copy Content", parameters);
        var result = await Modal.Result;
        if (result is { Confirmed: true, Data: CopyContentCommand copyContentModel })
        {
            var copyContentResult = await mediator.Send(copyContentModel);
            if (!copyContentResult.Success)
            {
                notificationService.ShowNotifications(copyContentResult.Messages);
            }
            else
            {
                notificationService.ShowSuccessNotification("Content copied");
            }
        }
    }

    public int SortOrder => -99;
}