using ZauberCMS.Core.Content.Interfaces;
using ZauberCMS.Core.Content.Models;

namespace ZauberCMS.Routing.Views.Shared.Components.RenderBlocks;

public class RenderBlocksModel
{
    public List<Content> Blocks { get; set; } = [];
    public string? SeparatorCssClasses { get; set; }
    public Dictionary<string, IContentBlockView> ContentBlockViews { get; set; } = new();
}