using Microsoft.AspNetCore.Mvc;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Routing.Views.Shared.Components.BootstrapPager;

public class BootstrapPagerViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(PaginatedList<Content> model, string baseUrl)
    {
        return View(model);
    }
}