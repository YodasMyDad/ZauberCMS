using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Media.Interfaces;
using ZauberCMS.Core.Media.Parameters;
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
            var mediaService = scope.ServiceProvider.GetRequiredService<IMediaService>();

            // Extract media ID or filename from the path
            var mediaPath = context.Request.Path.Value;

            // Check if this media item is restricted
            var isRestricted = await IsMediaRestrictedAsync(mediaService, mediaPath);

            if (isRestricted)
            {
                if (context.User.Identity?.IsAuthenticated == false)
                {
                    context.Response.StatusCode = 401;
                    context.Response.ContentType = "text/plain";
                    await context.Response.WriteAsync("Unauthorized access to media");
                    return;
                }
                else
                {
                    // User is authenticated, let ImageResize handle the request normally
                    // Add a marker to indicate authentication passed
                    context.Items["ZauberMediaAuthenticated"] = true;
                }
            }
        }

        await next(context);
    }

    private static async Task<bool> IsMediaRestrictedAsync(IMediaService mediaService, string? mediaPath)
    {
        if (!mediaPath.IsNullOrWhiteSpace())
        {
            var mediaDict = await mediaService.GetRestrictedMediaUrlsAsync(new GetRestrictedMediaUrlsParameters());

            // Check both with and without leading slash
            return mediaDict.ContainsKey(mediaPath) || mediaDict.ContainsKey(mediaPath.TrimStart('/'));
        }
        return false;
    }

}