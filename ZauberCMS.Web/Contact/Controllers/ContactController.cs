using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Settings;
using ZauberCMS.Routing.Controllers;
using ZauberCMS.Web.Shared.Models;

namespace ZauberCMS.Web.Contact.Controllers;

public class ContactController(ILogger<ContactController> logger, IOptions<ZauberSettings> options, IMediator mediator) 
    : ZauberRenderController(logger, options)
{
    public async Task<IActionResult> Contact()
    {
        var model = new SharedViewModel(CurrentPage!);

        model.HeaderImage = await model.GetMedia("HeaderImage", mediator, "/assets/img/contact-bg.jpg");
        
        return CurrentView(model);
    }
}