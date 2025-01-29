using Microsoft.AspNetCore.Http;
using ZauberCMS.Core.Content.Interfaces;
using ZauberCMS.Core.Plugins;

namespace ZauberCMS.Core.Content.ContentFinders;

public class ContentFinderPipeline(ExtensionManager extensionManager)
{
    public async Task<bool> FindContent(HttpContext context)
    {
        var contentFinders = extensionManager.GetInstances<IContentFinder>();
        
        foreach (var finder in contentFinders.Values)
        {
            if (await finder.TryFindContent(context))
            {
                return true; // Stop processing when content is found
            }
        }

        return false;
    }
}