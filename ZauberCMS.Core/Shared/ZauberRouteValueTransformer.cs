using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using ZauberCMS.Core.Content.Commands;

namespace ZauberCMS.Core.Shared;

public class ZauberRouteValueTransformer(IMediator mediator) : DynamicRouteValueTransformer
{
    public override async ValueTask<RouteValueDictionary> TransformAsync(HttpContext httpContext, RouteValueDictionary values)
    {
        var slug = httpContext.Request.Path.Value?.TrimStart('/');
        var url = httpContext.Request.GetDisplayUrl();
        var entryModel = await mediator.Send(new GetContentFromRequestCommand
        {
            Slug = slug,
            IsRootContent = string.IsNullOrWhiteSpace(slug),
            Url = url
        });

        if (entryModel.Content == null)
        {
            return values; // Use the default route if no content is found
        }

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
}
