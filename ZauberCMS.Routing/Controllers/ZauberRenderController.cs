using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ZauberCMS.Core.Content.Models;

namespace ZauberCMS.Routing.Controllers;

public class ZauberRenderController(ILogger<ZauberRenderController> logger) : Controller
{
    // ReSharper disable once InconsistentNaming
    private Content? _content { get; set; }
    // ReSharper disable once InconsistentNaming
    private Dictionary<string, string>? _languageKeys { get; set; }
    
    public virtual IActionResult Index()
    {
        if (CurrentPage != null)
        {
            return CurrentView(CurrentPage);
        }
        logger.LogInformation("No page found for route: {RoutePath}", ControllerContext.HttpContext.Request.Path);
        return NotFound();
    }

    protected Content? CurrentPage
    {
        get
        {
            if (_content == null)
            {
                if (HttpContext.Items.TryGetValue("currentpage", out var model) && model is Content content)
                {
                    _content = content;
                    //TempData["CurrentPage"] = _content;
                }
            }
            return _content;
        }
    }
    
    protected Dictionary<string, string>? LanguageKeys
    {
        get
        {
            if (_languageKeys == null)
            {
                if (HttpContext.Items.TryGetValue("languagekeys", out var model) && model is Dictionary<string, string> langKeys)
                {
                    _languageKeys = langKeys;
                    TempData["LanguageKeys"] = _languageKeys;
                }
            }
            return _languageKeys;
        }
    }
    
    /// <summary>
    ///     Locates the template for the given route
    /// </summary>
    /// <typeparam name="T">Model type</typeparam>
    /// <param name="model">Instance of model</param>
    /// <param name="viewName">View name</param>
    /// <returns>Template for given route</returns>
    protected IActionResult CurrentView<T>(T model, string? viewName = null)
    {
        if (string.IsNullOrEmpty(viewName))
        {
            viewName = ControllerContext.HttpContext.Items["viewpath"]?.ToString();
        }
        
        return View(viewName, model);
    }
    
    public IActionResult CurrentView()
    {
        return CurrentView(CurrentPage);
    }
}