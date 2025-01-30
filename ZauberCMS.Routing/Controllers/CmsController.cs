using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ZauberCMS.Core.Content.Models;

namespace ZauberCMS.Routing.Controllers;

public class CmsController(ILogger<CmsController> logger) : Controller
{
    // ReSharper disable once InconsistentNaming
    private Content? _content { get; set; }
    // ReSharper disable once InconsistentNaming
    private Dictionary<string, string>? _languageKeys { get; set; }
    
    public virtual IActionResult Index()
    {
        if (CurrentPage != null)
        {
            // TODO - Remove Index From Here
            return CurrentTemplate(CurrentPage, "Index");   
        }
        return NotFound();
    }

    public Content? CurrentPage
    {
        get
        {
            if (_content == null)
            {
                if (HttpContext.Items.TryGetValue("currentpage", out var model) && model is Content content)
                {
                    _content = content;
                }
            }
            return _content;
        }
    }
    
    public Dictionary<string, string>? LanguageKeys
    {
        get
        {
            if (_languageKeys == null)
            {
                if (HttpContext.Items.TryGetValue("languagekeys", out var model) && model is Dictionary<string, string> langKeys)
                {
                    _languageKeys = langKeys;
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
    public IActionResult CurrentTemplate<T>(T model, string? viewName)
    {
        if (string.IsNullOrEmpty(viewName))
        {
            viewName = ControllerContext.RouteData.Values["action"]?.ToString();
        }
        
        return View(viewName, model);
    }
}