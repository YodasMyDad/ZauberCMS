using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using MVCExample.Web.Models;
using ZauberCMS.Routing.Controllers;

namespace ZauberCMS.Web.Controllers;

public class HomeController(ILogger<HomeController> logger) : CmsController(logger)
{
    public override IActionResult Index()
    {
        // TODO - Remove Index From Here
        return CurrentTemplate(CurrentPage, "Index");   
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}