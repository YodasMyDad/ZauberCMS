using Blazored.Modal.Services;
using Microsoft.AspNetCore.Components;
using Radzen;
using ZauberCMS.Core;
using ZauberCMS.Core.Sections.Interfaces;

namespace ZauberCMS.Components.ContextMenus.SectionNavGroups;

public class CreateCompositionFolder : ISectionNavGroupAction
{
    public string Text => "Create Folder";
    public string Icon => "folder";
    public string IconColor => string.Empty;
    public string SectionNavGroupAlias => Constants.Sections.SectionNavGroups.StructureCompositionsNavGroup;
    public int SortOrder => -50;

    public Task ContextMenuAction(MenuItemEventArgs e, NavigationManager navigationManager, ContextMenuService contextMenuService,
        IModalService modalService)
    {
        contextMenuService.Close();
        navigationManager.NavigateTo($"{Urls.AdminStructureCreateFolder}/2", true);
        return Task.CompletedTask;
    }
}