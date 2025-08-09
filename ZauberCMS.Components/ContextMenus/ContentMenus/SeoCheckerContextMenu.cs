using Blazored.Modal;
using Blazored.Modal.Services;
using Microsoft.AspNetCore.Components;
using Radzen;
using ZauberCMS.Components.Seo;
using ZauberCMS.Core;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Content.Parameters;
using ZauberCMS.Core.Extensions;

namespace ZauberCMS.Components.ContextMenus.ContentMenus;

public class SeoCheckerContextMenu() : ITreeContextMenu
{
    public List<string> Sections => [Constants.Sections.ContentSection];
    public List<string> TreeAlias => [];
    public string Text(TreeItemContextMenuEventArgs args) => "Seo Checker";

    public string Icon(TreeItemContextMenuEventArgs args) => "rocket_launch";

    public string IconColor(TreeItemContextMenuEventArgs args) => string.Empty;

    public bool CanShowContextMenu(TreeItemContextMenuEventArgs args)
    {
        if(args.Value is Content content)
        {
            return content.Published && !content.ViewComponent.IsNullOrWhiteSpace();
        }
        return false;
    }

    private IModalReference? Modal { get; set; }
    private CopyContentParameters CopyContentCommand { get; set; } = new();
    
    public async Task ContextMenuAction(TreeItemContextMenuEventArgs args, MenuItemEventArgs e, NavigationManager navigationManager,
        ContextMenuService contextMenuService, IModalService modalService)
    {
        contextMenuService.Close();
        var content = (Content)args.Value!;
        CopyContentCommand.ContentToCopy = content.Id;
        var currentUri = new Uri(navigationManager.BaseUri);
        var fullUrl = $"{currentUri}{content.Url()}";
        var parameters = new Dictionary<string, object>
        {
            { nameof(SeoChecker.FullUrl), fullUrl},
            { nameof(SeoChecker.RunNow), true}
        };

        Modal = modalService.OpenSidePanel<SeoChecker>("SEO Checker", parameters);
        var result = await Modal.Result;
        /*if (result is { Confirmed: true, Data: CopyContentCommand copyContentModel })
        {

        }*/
    }

    public int SortOrder => 100;
}