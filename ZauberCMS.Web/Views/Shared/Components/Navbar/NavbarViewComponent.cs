using Microsoft.AspNetCore.Mvc;
using ZauberCMS.Core.Content.Models;

namespace ZauberCMS.Web.Views.Shared.Components.Navbar;

public class NavbarViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(Content website)
    {
        var currentPath = HttpContext.Request.Path.Value ?? "/";
        var model = new NavBarModel
        {
            Website = website,
            CurrentPath = currentPath
        };
        return View(model);
    }
}


