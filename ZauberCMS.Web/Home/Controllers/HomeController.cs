using MediatR;
using Microsoft.AspNetCore.Mvc;
using ZauberCMS.Core.Content.Commands;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Routing.Controllers;
using ZauberCMS.Web.Home.Models;

namespace ZauberCMS.Web.Home.Controllers;

public class HomeController(ILogger<HomeController> logger, IMediator mediator) : ZauberRenderController(logger)
{
    /// <summary>
    /// Route hijacked controller, 'Home' ContentType and 'Home' View 
    /// </summary>
    /// <returns></returns>
    public async Task<IActionResult> Home()
    {
        var homeViewModel = new HomeViewModel(CurrentPage!)
        {
            // Get the header image
            HeaderImage = await CurrentPage!.GetMedia("HeaderImage", mediator, "/assets/img/home-bg.jpg")
        };

        // Get the blog posts
        var posts = await mediator.Send(new QueryContentCommand
        {
            AmountPerPage = 4,
            ContentTypeAlias = "BlogPage",
            OrderBy = GetContentsOrderBy.DateUpdatedDescending,
            Cached = true
        });

        homeViewModel.BlogPosts = posts.Items.ToList();
        
        return CurrentView(homeViewModel);
    }

    public ActionResult Custom()
    {
        return View();
    }
}