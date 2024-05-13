using Microsoft.AspNetCore.Mvc;

namespace ZauberCMS.Web.Controllers;

public class HomeController : Controller
{
    // GET
    public IActionResult Index()
    {
        return View();
    }
}