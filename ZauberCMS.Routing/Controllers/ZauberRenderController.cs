using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using ZauberCMS.Core.Content.Models;

namespace ZauberCMS.Routing.Controllers;

public class ZauberRenderController(ILogger<ZauberRenderController> logger) : Controller
{
    private const string TransferredModelStateKey = "TransferredModelState";
    private const string TransferredViewDataKey = "TransferredViewData";
    
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
    
    protected IActionResult CurrentCmsPage()
    {
        // Get the URL of the page that issued the POST.
        var returnUrl = Request.Headers.Referer.ToString();
        return Redirect(string.IsNullOrEmpty(returnUrl) ? "/" : returnUrl);
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
    /// Locates the template for the given route.
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

    /// <summary>
    /// Restore any transferred ModelState and ViewData from TempData.
    /// </summary>
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        // Restore ModelState errors (if any)
        if (TempData.TryGetValue(TransferredModelStateKey, out var modelState))
        {
            var serializedModelState = modelState as string;
            if (!string.IsNullOrEmpty(serializedModelState))
            {
                var errors = JsonSerializer.Deserialize<Dictionary<string, string[]>>(serializedModelState);
                if (errors != null)
                {
                    foreach (var kvp in errors)
                    {
                        foreach (var error in kvp.Value)
                        {
                            ModelState.AddModelError(kvp.Key, error);
                        }
                    }
                }
            }
            TempData.Remove(TransferredModelStateKey);
        }
        
        // Restore entire ViewData
        if (TempData.TryGetValue(TransferredViewDataKey, out var viewData))
        {
            var serializedViewData = viewData as string;
            if (!string.IsNullOrEmpty(serializedViewData))
            {
                var viewDataDictionary = JsonSerializer.Deserialize<Dictionary<string, object>>(serializedViewData);
                if (viewDataDictionary != null)
                {
                    foreach (var kvp in viewDataDictionary)
                    {
                        ViewData[kvp.Key] = kvp.Value;
                    }
                }
            }
            TempData.Remove(TransferredViewDataKey);
        }
        
        base.OnActionExecuting(context);
    }

    /// <summary>
    /// Before redirecting, store ModelState and the entire ViewData into TempData.
    /// </summary>
    public override void OnActionExecuted(ActionExecutedContext context)
    {
        // Only if the result is a redirect do we transfer the data.
        if (context.Result is RedirectResult or RedirectToActionResult)
        {
            // Transfer ModelState errors if ModelState is invalid.
            if (!ModelState.IsValid)
            {
                var errorDictionary = ModelState
                    .Where(kvp => kvp.Value != null && kvp.Value.Errors.Any())
                    .ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value?.Errors.Select(e => e.ErrorMessage).ToArray());
                TempData[TransferredModelStateKey] = JsonSerializer.Serialize(errorDictionary);
            }

            // Transfer the entire ViewData dictionary.
            var viewDataDictionary = new Dictionary<string, object>();
            foreach (var kvp in ViewData)
            {
                viewDataDictionary[kvp.Key] = kvp.Value;
            }
            TempData[TransferredViewDataKey] = JsonSerializer.Serialize(viewDataDictionary);
        }
        
        base.OnActionExecuted(context);
    }
}