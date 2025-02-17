using Blazored.Modal.Services;
using Microsoft.AspNetCore.Components;
using Radzen;
using ZauberCMS.Components.Admin.SettingsSection.Dialogs;
using ZauberCMS.Core;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Languages.Models;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Components.Trees.ContextMenus.SettingsMenus;

public class CreateLanguageContextMenu() : ITreeContextMenu
{
    public List<string> Sections { get; } = [];
    public List<string> TreeAlias { get; } = [Constants.Sections.Trees.SettingsLanguagesTree];
    public string Text(TreeItemContextMenuEventArgs args) => "Create";

    public string Icon(TreeItemContextMenuEventArgs args) => string.Empty;

    public string IconColor(TreeItemContextMenuEventArgs args) => string.Empty;

    public bool CanShowContextMenu(TreeItemContextMenuEventArgs args)
    {
        if (args.Value is TreeStub treeStub)
        {
            if (treeStub.StubType == typeof(Language))
            {
                return true;   
            }
        }
        return false;
    }

    public async Task ContextMenuAction(TreeItemContextMenuEventArgs args, MenuItemEventArgs e, NavigationManager navigationManager,
        ContextMenuService contextMenuService, IModalService modalService)
    {
        contextMenuService.Close();
        var dialog = modalService.OpenSidePanel<CreateLanguage>("Create Language");
        var result = await dialog.Result;
        if (result is { Confirmed: true, Data: Language language })
        {
            // Do I need to do anything else here?
        }      
    }

    public int SortOrder => -90;
}