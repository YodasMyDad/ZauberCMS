using Blazored.Modal.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Radzen;
using ZauberCMS.Core;
using ZauberCMS.Core.Localization;
using ZauberCMS.Core.Sections.Interfaces;
using ZauberCMS.Core.Extensions;

namespace ZauberCMS.Components.ContextMenus.SectionNavGroups;

public class CreateCompositionNavMenu(IStringLocalizer<ZauberText> text) : ISectionNavGroupAction
{
    public string Text => text["contextMenus_createComposition"];
    public string Icon => "add";
    public string IconColor => string.Empty;
    public string SectionNavGroupAlias => Constants.Sections.SectionNavGroups.StructureCompositionsNavGroup;
    public int SortOrder => -100;

    public Task ContextMenuAction(MenuItemEventArgs e, NavigationManager navigationManager, ContextMenuService contextMenuService,
        IModalService modalService)
    {
        contextMenuService.Close();
        navigationManager.NavigateTo(navigationManager.ToSiteUrl(Urls.AdminStructureCreateComposition), true);
        return Task.CompletedTask;
    }
}