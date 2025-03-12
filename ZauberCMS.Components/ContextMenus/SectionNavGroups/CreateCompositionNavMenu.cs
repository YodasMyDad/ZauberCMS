using Blazored.Modal.Services;
using Microsoft.AspNetCore.Components;
using Radzen;
using ZauberCMS.Core;
using ZauberCMS.Core.Sections.Interfaces;

namespace ZauberCMS.Components.ContextMenus.SectionNavGroups;

public class CreateCompositionNavMenu : ISectionNavGroupAction
{
    public string Text => "Create Composition";
    public string Icon => "add";
    public string IconColor => string.Empty;
    public string SectionNavGroupAlias => Constants.Sections.SectionNavGroups.StructureCompositionsNavGroup;
    public int SortOrder => -100;

    public Task ContextMenuAction(MenuItemEventArgs e, NavigationManager navigationManager, ContextMenuService contextMenuService,
        IModalService modalService)
    {
        contextMenuService.Close();
        navigationManager.NavigateTo(Urls.AdminStructureCreateComposition, true);
        return Task.CompletedTask;
    }
}