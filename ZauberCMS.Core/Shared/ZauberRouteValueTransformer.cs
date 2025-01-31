using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using ZauberCMS.Core.Content.ContentFinders;

namespace ZauberCMS.Core.Shared;

public class ZauberRouteValueTransformer(ContentFinderPipeline contentFinderPipeline) : DynamicRouteValueTransformer
{
    public override async ValueTask<RouteValueDictionary> TransformAsync(HttpContext httpContext, RouteValueDictionary values)
    {
        return await contentFinderPipeline.FindContent(httpContext, values);
    }
}
