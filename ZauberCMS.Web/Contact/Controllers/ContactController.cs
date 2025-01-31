using MediatR;
using Microsoft.AspNetCore.Mvc;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Routing.Controllers;
using ZauberCMS.Web.Shared.Models;

namespace ZauberCMS.Web.Contact.Controllers;

public class ContactController(ILogger<ContactController> logger, IMediator mediator) 
    : ZauberRenderController(logger)
{
    public async Task<IActionResult> Contact()
    {
        var model = new SharedViewModel(CurrentPage!);
        
        model.HeaderImage = (await model.GetMedias("HeaderImage", mediator, "/assets/img/contact-bg.jpg")).FirstOrDefault()!;
        
        return CurrentView(model);
    }
}