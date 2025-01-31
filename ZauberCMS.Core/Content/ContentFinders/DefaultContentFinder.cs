using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Routing;
using ZauberCMS.Core.Content.Commands;
using ZauberCMS.Core.Content.Interfaces;
using ZauberCMS.Core.Extensions;

namespace ZauberCMS.Core.Content.ContentFinders;

public class DefaultContentFinder(IMediator mediator) 
    : IContentFinder
{
    public async Task<RouteValueDictionary?> TryFindContent(HttpContext httpContext)
    {
        var slug = httpContext.Request.Path.Value?.TrimStart('/');
        var url = httpContext.Request.GetDisplayUrl();
        var entryModel = await mediator.Send(new GetContentFromRequestCommand
        {
            Slug = slug, 
            IsRootContent = slug.IsNullOrWhiteSpace(), 
            Url = url
        });
        
        if (entryModel.Content == null) return null;
        
        var controllerName = entryModel.Content.ContentTypeAlias;
        var actionName = Path.GetFileNameWithoutExtension(entryModel.Content.ViewComponent);

        var newValues = new RouteValueDictionary
        {
            ["controller"] = controllerName,
            ["action"] = actionName
        };
        
        httpContext.Items["currentpage"] = entryModel.Content;
        httpContext.Items["languagekeys"] = entryModel.LanguageKeys;
        httpContext.Items["viewpath"] = entryModel.Content.ViewComponent;
        
        return newValues;
    }

    public int SortOrder => 10;
}