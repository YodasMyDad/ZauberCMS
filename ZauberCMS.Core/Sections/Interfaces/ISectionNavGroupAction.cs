using Blazored.Modal.Services;
using Microsoft.AspNetCore.Components;
using Radzen;

namespace ZauberCMS.Core.Sections.Interfaces;

public interface ISectionNavGroupAction
{
    string Text { get; }
    string Icon { get; }
    string IconColor { get; }
    string SectionNavGroupAlias { get; }
    int SortOrder { get; }
    Task ContextMenuAction(MenuItemEventArgs e, NavigationManager navigationManager, ContextMenuService contextMenuService, IModalService modalService);
}