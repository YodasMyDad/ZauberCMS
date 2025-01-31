using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using ZauberCMS.Core.Content.Interfaces;
using ZauberCMS.Core.Plugins;

namespace ZauberCMS.Core.Content.ContentFinders;

public class ContentFinderPipeline(ExtensionManager extensionManager)
{
    public async Task<RouteValueDictionary> FindContent(HttpContext context, RouteValueDictionary defaultRouteValueDictionary)
    {
        var contentFinders = extensionManager.GetInstances<IContentFinder>(true);
        
        foreach (var finder in contentFinders.Values.OrderBy(x => x.SortOrder))
        {
            var routeValueDictionary = await finder.TryFindContent(context);
            if(routeValueDictionary != null)
            {
                return routeValueDictionary; // Return the new route values if found
            }
        }

        return defaultRouteValueDictionary; // If nothing found, return the default route values
    }
}