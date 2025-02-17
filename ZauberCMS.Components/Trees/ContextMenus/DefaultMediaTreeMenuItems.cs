using Blazored.Modal;
using Blazored.Modal.Services;
using MediatR;
using Microsoft.AspNetCore.Components;
using Radzen;
using ZauberCMS.Components.Admin.MediaSection.Dialogs;
using ZauberCMS.Components.Editors;
using ZauberCMS.Core;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Media.Commands;
using ZauberCMS.Core.Media.Models;

namespace ZauberCMS.Components.Trees.ContextMenus;

public class DefaultMediaTreeMenuItems(
    IMediator mediator,
    NotificationService notificationService) : ITreeContextMenu
{
    private IModalReference? CreateFolderModal { get; set; }
    private IModalReference? UpdateMediaModal { get; set; }
    public List<string> Sections => [Constants.Sections.MediaSection];
    public List<string> TreeAlias { get; } = [];

    public List<ContextMenuItem> ContentMenuItems(TreeItemContextMenuEventArgs args)
    {
        var items = new List<ContextMenuItem>();
        
        if (args.Value is Media media)
        {
            if (media.MediaType == MediaType.Folder)
            {
                items.Add(new ContextMenuItem { Text = "Create Folder", Value = 1 });
                items.Add(new ContextMenuItem { Text = "Upload Media", Value = 2 });   
            }
            items.Add(new ContextMenuItem { Text = "Delete", Value = 3 });
        }
        
        return items;
    }

    private void OnFolderCreate(Media value)
    {
        CreateFolderModal?.Close();
    }
    
    private void OnUploadMedia(List<Media> value)
    {
        UpdateMediaModal?.Close();
    }
    
    public async Task ContextMenuEvents(TreeItemContextMenuEventArgs args,
        MenuItemEventArgs e,
        NavigationManager navigationManager,
        ContextMenuService contextMenuService,
        IModalService modalService)
    {
        if (args.Value is Media media)
        {
            /*var dbContent = await Mediator.Send(new GetMediaCommand { Id = media.Id, IncludeChildren = true });*/
            //var text = args.Text;
            switch (e.Value)
            {
                case 1:
                    var createParams = new Dictionary<string, object>
                    {
                        { nameof(UpdateMediaForm.ParentId), media.Id },
                        {
                            nameof(UpdateMediaForm.ValueChanged),
                            EventCallback.Factory.Create<Media>(this, OnFolderCreate)
                        }
                    };
                    contextMenuService.Close();
                    CreateFolderModal = modalService?.OpenSidePanel<UpdateMediaForm>("Create Folder", createParams);
                    break;
                case 2:
                    var uploadParams = new Dictionary<string, object>
                    {
                        { nameof(MultipleMediaUpload.ParentId), media.Id },
                        {
                            nameof(MultipleMediaUpload.ValueChanged),
                            EventCallback.Factory.Create<List<Media>>(this, OnUploadMedia)
                        }
                    };
                    contextMenuService.Close();
                    UpdateMediaModal = modalService?.OpenSidePanel<MultipleMediaUpload>("Upload Media", uploadParams);
                    break;
                case 3:
                    var deleteResult = await mediator.Send(new DeleteMediaCommand { MediaId = media.Id });
                    if (deleteResult.Success)
                    {
                        // Only redirect if on item being deleted? How do we check that?
                        //NavigationManager.NavigateTo("/admin/media", forceLoad: true);
                        notificationService.ShowSuccessNotification("Media deleted");
                    }
                    else
                    {
                        notificationService.ShowErrorNotification(deleteResult.Messages.MessagesAsString());
                    }

                    break;
            }
        }
    }

    public int SortOrder => -50;
}