using MediatR;
using Microsoft.AspNetCore.Mvc;
using ZauberCMS.Routing.Controllers;

namespace ZauberCMS.Web.Blog.Controllers;

public class BlogPageController(ILogger<BlogPageController> logger, IMediator mediator) : ZauberRenderController(logger)
{
    public async Task<IActionResult> BlogPage()
    {
        return CurrentView();
    }
}