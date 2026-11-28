using Blazored.Modal.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Radzen;
using ZauberCMS.Core;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Localization;

namespace ZauberCMS.Components.ContextMenus.ContentMenus;

public class CreateContentContextMenu(IStringLocalizer<ZauberText> text) : ITreeContextMenu
{
    public List<string> Sections => [Constants.Sections.ContentSection];
    public List<string> TreeAlias { get; } = [];
    public string Text(TreeItemContextMenuEventArgs args) => text["general_create"];
    public string Icon(TreeItemContextMenuEventArgs args) => "add";
    public string IconColor(TreeItemContextMenuEventArgs args) => string.Empty;
    public bool CanShowContextMenu(TreeItemContextMenuEventArgs args)
    {
        if(args.Value is Content content)
        {
            return content.Published;
        }
        return false;
    }

    public Task ContextMenuAction(TreeItemContextMenuEventArgs args, MenuItemEventArgs e, NavigationManager navigationManager,
        ContextMenuService contextMenuService, IModalService modalService)
    {
        var content = (Content)args.Value!;
        contextMenuService.Close();
        navigationManager.NavigateTo(navigationManager.ToSiteUrl($"{Urls.AdminCreateContent}/{content.Id}"));
        return Task.CompletedTask;
    }

    public int SortOrder => -100;
}