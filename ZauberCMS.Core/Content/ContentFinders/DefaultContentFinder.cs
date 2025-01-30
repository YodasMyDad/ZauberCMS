using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Options;
using ZauberCMS.Core.Content.Commands;
using ZauberCMS.Core.Content.Interfaces;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Settings;

namespace ZauberCMS.Core.Content.ContentFinders;

public class DefaultContentFinder(IMediator mediator, IOptions<ZauberSettings> Settings) 
    : IContentFinder
{
    public async Task<bool> TryFindContent(HttpContext context)
    {
        var slug = context.Request.Path.Value?.TrimStart('/');
        var url = context.Request.GetDisplayUrl();
        var entryModel = await mediator.Send(new GetContentFromRequestCommand { Slug = slug, IsRootContent = slug.IsNullOrWhiteSpace(), Url = url });
        
        if (entryModel.Content == null) return false;
        
        context.Request.RouteValues["controller"] = entryModel.Content.ContentTypeAlias;
        
        context.Request.RouteValues["action"] = Path.GetFileNameWithoutExtension(entryModel.Content.ViewComponent);

        context.Items["currentpage"] = entryModel.Content;
        context.Items["languagekeys"] = entryModel.LanguageKeys;
        context.Items["viewpath"] = entryModel.Content.ViewComponent;
        
        return true;
    }
}