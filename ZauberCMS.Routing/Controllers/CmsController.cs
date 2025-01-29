using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ZauberCMS.Core.Content.Models;

namespace ZauberCMS.Routing.Controllers;

public class CmsController(ILogger<CmsController> logger) : Controller
{
    /*public IActionResult Index()
    {
        // Get the request path and remove the leading slash if present
        var slug = HttpContext.Request.Path.Value?.TrimStart('/');
        logger.LogInformation("Testing this works on CMS controller");
        return View("/Views/Cms/Index.cshtml");
    }*/
    
    public IActionResult Index(Content? model)
    {
        if (model == null) return NotFound();
        // TODO - Need to pass in the ContentView here as if we get here there is no custom controller
        var viewName = ControllerContext.RouteData.Values["action"]?.ToString() ?? "Index";
        return View(viewName, model);
    }
}