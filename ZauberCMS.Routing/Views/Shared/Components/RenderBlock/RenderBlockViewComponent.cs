using Microsoft.AspNetCore.Mvc;
using ZauberCMS.Core.Content.Models;
using RenderMode = Microsoft.AspNetCore.Mvc.Rendering.RenderMode;

namespace ZauberCMS.Routing.Views.Shared.Components.RenderBlock;

public class RenderBlockViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(Content content, RenderMode renderMode = RenderMode.Static)
    {
        var renderBlockModel = new RenderBlockModel
        {
            Content = content,
            RenderMode = renderMode
        };
        return View(renderBlockModel);
    }
}