using MediatR;
using Microsoft.AspNetCore.Mvc;
using ZauberCMS.Routing.Controllers;

namespace ZauberCMS.Web.Blog.Controllers;

public class BlogController(ILogger<BlogController> logger, IMediator mediator) : ZauberRenderController(logger)
{
    public async Task<IActionResult> Blog()
    {
        return CurrentView();
    }
}