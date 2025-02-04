using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ZauberCMS.Core.Content.Commands;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Plugins;
using ZauberCMS.Core.Rendering;
using ZauberCMS.Core.Settings;
using ZauberCMS.Routing.Controllers;
using ZauberCMS.Web.Home.Models;

namespace ZauberCMS.Web.Home.Controllers;

public class HomeController(ILogger<HomeController> logger, IOptions<ZauberSettings> options, IMediator mediator, ExtensionManager extensionManager) 
    : ZauberRenderController(logger, options, mediator)
{
    private readonly IMediator _mediator = mediator;

    /// <summary>
    /// Route hijacked controller, 'Home' ContentType and 'Home' View 
    /// </summary>
    /// <returns></returns>
    public async Task<IActionResult> Home()
    {
        var homeViewModel = new HomeViewModel(CurrentPage!)
        {
            // Get the header image
            HeaderImage = await CurrentPage!.GetMedia("HeaderImage", _mediator, "/assets/img/home-bg.jpg")
        };

        // Get the blog posts
        var posts = await _mediator.Send(new QueryContentCommand
        {
            AmountPerPage = 4,
            ContentTypeAlias = "BlogPage",
            OrderBy = GetContentsOrderBy.DateUpdatedDescending,
            Cached = true
        });

        homeViewModel.BlogPosts = posts.Items.ToList();
        
        return CurrentView(homeViewModel);
    }
    
    public IActionResult Error(int? statusCode)
    {
        if (statusCode.HasValue)
        {
            if (statusCode == 404)
            {
                return View("NotFound"); // Custom 404 page
            }
        }
        return View("Error"); // Generic error page
    }

    public ActionResult Custom()
    {
        return View();
    }
}