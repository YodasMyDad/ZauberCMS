using Blazored.Modal;
using Blazored.Modal.Services;
using Microsoft.AspNetCore.Components;
using Radzen;
using ZauberCMS.Components.Editors;
using ZauberCMS.Core;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Media.Models;

namespace ZauberCMS.Components.Trees.ContextMenus.MediaMenus;

public class UploadMediaContextMenu() : ITreeContextMenu
{
    public List<string> Sections => [Constants.Sections.ContentSection];
    public List<string> TreeAlias { get; } = [];
    public string Text(TreeItemContextMenuEventArgs args) => "Upload Media";
    public string Icon(TreeItemContextMenuEventArgs args) => string.Empty;
    public string IconColor(TreeItemContextMenuEventArgs args) => string.Empty;
    public bool CanShowContextMenu(TreeItemContextMenuEventArgs args)
    {
        return args.Value is Media { MediaType: MediaType.Folder };
    }
    
    private IModalReference? UpdateMediaModal { get; set; }
    
    public Task ContextMenuAction(TreeItemContextMenuEventArgs args, MenuItemEventArgs e, NavigationManager navigationManager,
        ContextMenuService contextMenuService, IModalService modalService)
    {
        var media = (Media)args.Value!;
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
        return Task.CompletedTask;
    }
    
    private void OnUploadMedia(List<Media> value)
    {
        UpdateMediaModal?.Close();
    }
    
    public int SortOrder => -90;
}