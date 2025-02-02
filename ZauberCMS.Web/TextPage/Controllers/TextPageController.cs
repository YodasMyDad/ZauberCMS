using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Settings;
using ZauberCMS.Routing.Controllers;
using ZauberCMS.Web.Shared.Models;

namespace ZauberCMS.Web.TextPage.Controllers;

public class TextPageController(ILogger<TextPageController> logger, IOptions<ZauberSettings> options, IMediator mediator) 
    : ZauberRenderController(logger, options)
{
    public async Task<IActionResult> TextPage()
    {
        var model = new SharedViewModel(CurrentPage!);

        model.HeaderImage = await model.GetMedia("HeaderImage", mediator, "/assets/img/about-bg.jpg");
        
        return CurrentView(model);
    }
}