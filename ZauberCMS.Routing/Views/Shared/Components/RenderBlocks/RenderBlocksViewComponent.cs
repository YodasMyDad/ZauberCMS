using Microsoft.AspNetCore.Mvc;
using ZauberCMS.Core.Content.Interfaces;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Plugins;

namespace ZauberCMS.Routing.Views.Shared.Components.RenderBlocks;

public class RenderBlocksViewComponent(ExtensionManager extensionManager) : ViewComponent
{
    public IViewComponentResult Invoke(List<Content> blocks, string cssSeparatorClasses)
    {
        var contentBlockViews = extensionManager.GetInstances<IContentBlockView>();
        
        var renderBlockModel = new RenderBlocksModel
        {
            SeparatorCssClasses = cssSeparatorClasses,
            Blocks = blocks,
            ContentBlockViews = contentBlockViews
                .Select(x => x.Value)
                .DistinctBy(x => x.ContentTypeAlias)
                .ToDictionary(x => x.ContentTypeAlias, x => x)
        };
        return View(renderBlockModel);
    }
}