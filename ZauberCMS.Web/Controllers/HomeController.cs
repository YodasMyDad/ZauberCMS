using Microsoft.AspNetCore.Mvc;
using ZauberCMS.Routing.Controllers;

namespace ZauberCMS.Web.Controllers;

public class HomeController : CmsController
{
    public HomeController(ILogger<HomeController> logger) : base(logger)
    {
        Console.WriteLine("HomeController has been hit!");

    }

    public IActionResult Home()
    {
        Console.WriteLine("Home has been hit!");
        return CurrentView();
    }
}