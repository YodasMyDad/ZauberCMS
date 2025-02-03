using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ZauberCMS.Core.Content.Commands;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Settings;
using ZauberCMS.Routing.Controllers;
using ZauberCMS.Web.Blog.Models;

namespace ZauberCMS.Web.Blog.Controllers;

public class BlogPageController(ILogger<BlogPageController> logger, IOptions<ZauberSettings> options, IMediator mediator) 
    : ZauberRenderController(logger,options, mediator)
{
    private readonly IMediator _mediator = mediator;

    public async Task<IActionResult> BlogPage()
    {
        var viewModel = new BlogPageViewModel(CurrentPage!);
        
        viewModel.HeaderImage = await viewModel.GetMedia("HeaderImage", _mediator, "/assets/img/post-sample-image.jpg");
        
        return CurrentView(viewModel);
    }
}