﻿using Blazored.Modal.Services;
using Microsoft.AspNetCore.Components;
using Radzen;
using ZauberCMS.Core;
using ZauberCMS.Core.Content.Models;

namespace ZauberCMS.Components.Trees.ContextMenus.ContentMenus;

public class CreateContentContextMenu : ITreeContextMenu
{
    public List<string> Sections => [Constants.Sections.ContentSection];
    public List<string> TreeAlias { get; } = [];
    public string Text(TreeItemContextMenuEventArgs args) => "Create";
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
        navigationManager.NavigateTo($"{Urls.AdminCreateContent}/{content.Id}");
        return Task.CompletedTask;
    }

    public int SortOrder => -100;
}