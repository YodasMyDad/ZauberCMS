using MediatR;
using Microsoft.AspNetCore.Mvc;
using ZauberCMS.Core.Content.Commands;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Routing.Controllers;
using ZauberCMS.Web.Blog.Models;

namespace ZauberCMS.Web.Blog.Controllers;

public class BlogPageController(ILogger<BlogPageController> logger, IMediator mediator) : ZauberRenderController(logger)
{
    public async Task<IActionResult> BlogPage()
    {
        var viewModel = new BlogPageViewModel(CurrentPage!);
        
        viewModel.HeaderImage = await viewModel.GetMedia("HeaderImage", mediator, "/assets/img/post-sample-image.jpg");
        
        return CurrentView(viewModel);
    }
}