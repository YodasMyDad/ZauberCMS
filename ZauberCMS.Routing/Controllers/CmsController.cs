using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace ZauberCMS.Routing.Controllers;

public class CmsController(ILogger<CmsController> logger) : Controller
{
    public IActionResult Index(string slug)
    {
        logger.LogInformation("Testing this works on CMS controller");
        return View("/Views/Cms/Index.cshtml");
    }
}