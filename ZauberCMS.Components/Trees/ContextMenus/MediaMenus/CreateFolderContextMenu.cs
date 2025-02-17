using Blazored.Modal;
using Blazored.Modal.Services;
using Microsoft.AspNetCore.Components;
using Radzen;
using ZauberCMS.Components.Admin.MediaSection.Dialogs;
using ZauberCMS.Core;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Media.Models;

namespace ZauberCMS.Components.Trees.ContextMenus.MediaMenus;

public class CreateFolderContextMenu() : ITreeContextMenu
{
    public List<string> Sections => [Constants.Sections.MediaSection];
    public List<string> TreeAlias { get; } = [];
    public string Text(TreeItemContextMenuEventArgs args) => "Create Folder";
    public string Icon(TreeItemContextMenuEventArgs args) => string.Empty;
    public string IconColor(TreeItemContextMenuEventArgs args) => string.Empty;
    public bool CanShowContextMenu(TreeItemContextMenuEventArgs args)
    {
        return args.Value is Media { MediaType: MediaType.Folder };
    }

    private IModalReference? CreateFolderModal { get; set; }
    
    public Task ContextMenuAction(TreeItemContextMenuEventArgs args, MenuItemEventArgs e, NavigationManager navigationManager,
        ContextMenuService contextMenuService, IModalService modalService)
    {
        var media = (Media)args.Value!;
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
        return Task.CompletedTask;
    }
    
    private void OnFolderCreate(Media value)
    {
        CreateFolderModal?.Close();
    }

    public int SortOrder => -100;
}