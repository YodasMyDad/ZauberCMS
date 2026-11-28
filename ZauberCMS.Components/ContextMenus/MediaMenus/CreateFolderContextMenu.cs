using Blazored.Modal;
using Blazored.Modal.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Radzen;
using ZauberCMS.Components.Admin.MediaSection.Dialogs;
using ZauberCMS.Core;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Localization;
using ZauberCMS.Core.Media.Models;

namespace ZauberCMS.Components.ContextMenus.MediaMenus;

public class CreateFolderContextMenu(IStringLocalizer<ZauberText> text) : ITreeContextMenu
{
    public List<string> Sections => [Constants.Sections.MediaSection];
    public List<string> TreeAlias { get; } = [];
    public string Text(TreeItemContextMenuEventArgs args) => text["contextMenus_createFolder"];
    public string Icon(TreeItemContextMenuEventArgs args) => "add";
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
        CreateFolderModal = modalService?.OpenSidePanel<UpdateMediaForm>(text["contextMenus_createFolder"], createParams);
        return Task.CompletedTask;
    }
    
    private void OnFolderCreate(Media value)
    {
        CreateFolderModal?.Close();
    }

    public int SortOrder => -100;
}