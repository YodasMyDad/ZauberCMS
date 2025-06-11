using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Media.Commands;
using ZauberCMS.Core.Settings;

namespace ZauberCMS.Core.Media.Middleware;

public class RestrictedMediaMiddleware(RequestDelegate next, IServiceProvider serviceProvider, IOptions<ZauberSettings> settings)
{
    public async Task InvokeAsync(HttpContext context)
    {
        // Check if the request is for a media file
        if (context.Request.Path.StartsWithSegments($"/{settings.Value.UploadFolderName}", StringComparison.OrdinalIgnoreCase))
        {
            using var scope = serviceProvider.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            
            // Extract media ID or filename from the path
            var mediaPath = context.Request.Path.Value;
            
            // Check if this media item is restricted
            // You'll need to implement this logic based on your data structure
            var isRestricted = await IsMediaRestrictedAsync(mediator, mediaPath);
            
            if (isRestricted && context.User.Identity?.IsAuthenticated == false)
            {
                context.Response.StatusCode = 401;
                await context.Response.WriteAsync("Unauthorized access to media");
                return;
            }
        }

        await next(context);
    }

    private static async Task<bool> IsMediaRestrictedAsync(IMediator mediator, string? mediaPath)
    {
        if (!mediaPath.IsNullOrWhiteSpace())
        {
            var mediaDict = await mediator.Send(new GetRestrictedMediaUrls());
            return mediaDict.ContainsKey(mediaPath);

        }
        return false;
    }
}