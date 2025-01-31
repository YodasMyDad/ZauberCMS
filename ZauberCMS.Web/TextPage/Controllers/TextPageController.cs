using MediatR;
using Microsoft.AspNetCore.Mvc;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Routing.Controllers;
using ZauberCMS.Web.Shared.Models;

namespace ZauberCMS.Web.TextPage.Controllers;

public class TextPageController(ILogger<TextPageController> logger, IMediator mediator) : ZauberRenderController(logger)
{
    public async Task<IActionResult> TextPage()
    {
        var homeViewModel = new SharedViewModel(CurrentPage!);
        
        homeViewModel.HeaderImage = (await homeViewModel.GetMedias("HeaderImage", mediator, "/assets/img/about-bg.jpg")).FirstOrDefault()!;
        
        return CurrentView(homeViewModel);
    }
}