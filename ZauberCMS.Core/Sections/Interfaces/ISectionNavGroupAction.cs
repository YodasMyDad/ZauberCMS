using Blazored.Modal.Services;
using Microsoft.AspNetCore.Components;
using Radzen;

namespace ZauberCMS.Core.Sections.Interfaces;

public interface ISectionNavGroupAction
{
    public string Text { get; }
    public string Icon { get; }
    public string IconColor { get; }
    public string SectionNavGroupAlias { get; }
    Task ContextMenuAction(MenuItemEventArgs e, NavigationManager navigationManager, ContextMenuService contextMenuService, IModalService modalService);
}