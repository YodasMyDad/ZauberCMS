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
        
        //Content = entryModel.Content;
        //LanguageKeys = entryModel.LanguageKeys;
        if (entryModel.Content == null) return false;
        
        context.Request.RouteValues["controller"] = entryModel.Content.ContentTypeAlias;
        context.Request.RouteValues["action"] = "Index"; // TODO - This will be the chosen template/view
        
        // TODO - This should be a RenderModel that has the content? How do we get the language keys?
        context.Request.RouteValues["model"] = entryModel.Content;

        return true;
    }
}