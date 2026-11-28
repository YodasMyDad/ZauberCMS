using Blazored.Modal.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Radzen;
using ZauberCMS.Core;
using ZauberCMS.Core.Localization;
using ZauberCMS.Core.Sections.Interfaces;
using ZauberCMS.Core.Extensions;

namespace ZauberCMS.Components.ContextMenus.SectionNavGroups;

public class CreateElementTypeFolder(IStringLocalizer<ZauberText> text) : ISectionNavGroupAction
{
    public string Text => text["contextMenus_createFolder"];
    public string Icon => "folder";
    public string IconColor => string.Empty;
    public string SectionNavGroupAlias => Constants.Sections.SectionNavGroups.StructureElementTypesNavGroup;
    public int SortOrder => -50;

    public Task ContextMenuAction(MenuItemEventArgs e, NavigationManager navigationManager, ContextMenuService contextMenuService,
        IModalService modalService)
    {
        contextMenuService.Close();
        navigationManager.NavigateTo(navigationManager.ToSiteUrl($"{Urls.AdminStructureCreateFolder}/1"), true);
        return Task.CompletedTask;
    }
}