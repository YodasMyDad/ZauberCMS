using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Content.ContentFinders;

namespace ZauberCMS.Core.Shared.Middleware;

public class ContentRoutingMiddleware(RequestDelegate next, IServiceProvider serviceProvider)
{
    public async Task Invoke(HttpContext context)
    {
        using var scope = serviceProvider.CreateScope();
        var pipeline = scope.ServiceProvider.GetRequiredService<ContentFinderPipeline>();
        
        if (!await pipeline.FindContent(context))
        {
            await next(context); // Proceed if no content was found
            return;
        }

        await next(context);
    }
}