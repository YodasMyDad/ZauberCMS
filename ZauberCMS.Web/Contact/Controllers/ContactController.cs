using MediatR;
using Microsoft.AspNetCore.Mvc;
using ZauberCMS.Routing.Controllers;

namespace ZauberCMS.Web.Contact.Controllers;

public class ContactController(ILogger<ContactController> logger, IMediator mediator) : ZauberRenderController(logger)
{
    public async Task<IActionResult> Contact()
    {
        return CurrentView();
    }
}