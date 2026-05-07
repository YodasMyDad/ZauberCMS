using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ZauberCMS.Core;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Media.Interfaces;
using ZauberCMS.Core.Media.Models;
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

            // Extract media path from the request
            var mediaPath = context.Request.Path.Value;

            // Look up access metadata (id + allowed role names) for this media item
            var entry = await GetRestrictedMediaEntryAsync(mediaService, mediaPath);

            if (entry is not null)
            {
                if (context.User.Identity?.IsAuthenticated != true)
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    context.Response.ContentType = "text/plain";
                    await context.Response.WriteAsync("Unauthorized access to media");
                    return;
                }

                // Empty role list means "any authenticated user"; otherwise enforce role membership.
                // Admins are always allowed (matches EntryPage role-check pattern for content).
                if (entry.AllowedRoleNames.Count > 0 &&
                    !context.User.IsInRole(Constants.Roles.AdminRoleName) &&
                    !entry.AllowedRoleNames.Any(r => !string.IsNullOrEmpty(r) && context.User.IsInRole(r)))
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    context.Response.ContentType = "text/plain";
                    await context.Response.WriteAsync("Forbidden");
                    return;
                }

                // Authentication (and role check, if any) passed - let downstream middleware handle the request
                context.Items["ZauberMediaAuthenticated"] = true;
            }
        }

        await next(context);
    }

    private static async Task<RestrictedMediaEntry?> GetRestrictedMediaEntryAsync(IMediaService mediaService, string? mediaPath)
    {
        if (mediaPath.IsNullOrWhiteSpace())
        {
            return null;
        }

        var mediaDict = await mediaService.GetRestrictedMediaAccessAsync(new GetRestrictedMediaUrlsParameters());

        if (mediaDict.TryGetValue(mediaPath, out var entry))
        {
            return entry;
        }

        var trimmed = mediaPath.TrimStart('/');
        return mediaDict.TryGetValue(trimmed, out var entryTrimmed) ? entryTrimmed : null;
    }
}
