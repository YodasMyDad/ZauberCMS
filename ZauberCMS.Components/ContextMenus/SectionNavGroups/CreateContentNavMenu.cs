using Blazored.Modal.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Radzen;
using ZauberCMS.Core;
using ZauberCMS.Core.Localization;
using ZauberCMS.Core.Sections.Interfaces;
using ZauberCMS.Core.Extensions;

namespace ZauberCMS.Components.ContextMenus.SectionNavGroups;

public class CreateContentNavMenu(IStringLocalizer<ZauberText> text) : ISectionNavGroupAction
{
    public string Text => text["contextMenus_createContent"];
    public string Icon => "add";
    public string IconColor => string.Empty;
    public string SectionNavGroupAlias => Constants.Sections.SectionNavGroups.ContentNavGroup;
    public int SortOrder => -100;

    public Task ContextMenuAction(MenuItemEventArgs e, NavigationManager navigationManager, ContextMenuService contextMenuService,
        IModalService modalService)
    {
        contextMenuService.Close();
        navigationManager.NavigateTo(navigationManager.ToSiteUrl(Urls.AdminCreateContent), true);
        return Task.CompletedTask;
    }
}