using Microsoft.AspNetCore.Mvc;
using ZauberCMS.Core.Content.Interfaces;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Plugins;

namespace ZauberCMS.Routing.Views.Shared.Components.RenderCustomContent;

public class RenderCustomContentViewComponent(ExtensionManager extensionManager) : ViewComponent
{
    public IViewComponentResult Invoke(Content content, List<string> components)
    {
        var viewModel = new RenderCustomContentViewModel
        {
            Content = content,
            CustomContentComponents = extensionManager.GetInstances<ICustomContentComponent>(true),
            Components = components
        };
        return View(viewModel);
    }
}