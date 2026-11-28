using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ZauberCMS.Core;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Media.Interfaces;
using ZauberCMS.Core.Media.Models;
using ZauberCMS.Core.Media.Parameters;
using ZauberCMS.Core.Sections.Interfaces;
using ZauberCMS.Core.Settings;

namespace ZauberCMS.Core.Media.Middleware;

internal class RestrictedMediaMiddleware(RequestDelegate next, IServiceProvider serviceProvider, IOptions<ZauberSettings> settings)
{
    public async Task InvokeAsync(HttpContext context)
    {
        // Check if the request is for a media file. Compared in normalised form: the static file handler serves
        // "//media/a.png" and "/media//a.png" as "/media/a.png", so a raw prefix test could be walked round.
        var uploadFolder = Services.MediaService.NormaliseMediaPath(settings.Value.UploadFolderName).TrimEnd('/') + "/";
        if (Services.MediaService.NormaliseMediaPath(context.Request.Path.Value)
            .StartsWith(uploadFolder, StringComparison.Ordinal))
        {
            using var scope = serviceProvider.CreateScope();
            var mediaService = scope.ServiceProvider.GetRequiredService<IMediaService>();

            // Extract media path from the request
            var mediaPath = context.Request.Path.Value;

            // Look up access metadata (id + allowed role names) for this media item
            var entry = await GetRestrictedMediaEntryAsync(mediaService, mediaPath);

            if (entry is not null)
            {
                // The people who manage media: the Media section shows them recycled and restricted files alike
                // (Roadmap D67; before D38 that was administrators only)
                var mayManageMedia = context.User.Identity?.IsAuthenticated == true &&
                                     await scope.ServiceProvider.GetRequiredService<IBackOfficeAccessService>()
                                         .CanOpenSectionAsync(context.User, Constants.Sections.MediaSection,
                                             context.RequestAborted);

                // Recycled media is not served, except to the people who manage media (the recycle bin shows it)
                if (entry.InRecycleBin && !mayManageMedia)
                {
                    context.Response.StatusCode = StatusCodes.Status404NotFound;
                    return;
                }

                if (context.User.Identity?.IsAuthenticated != true)
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    context.Response.ContentType = "text/plain";
                    await context.Response.WriteAsync("Unauthorized access to media");
                    return;
                }

                // Empty role list means "any authenticated user"; otherwise enforce role membership.
                // Administrators and the others who may open the Media section are always allowed.
                if (entry.AllowedRoleNames.Count > 0 &&
                    !mayManageMedia &&
                    !entry.AllowedRoleNames.Any(r => !string.IsNullOrEmpty(r) && context.User.IsInRole(r)))
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    context.Response.ContentType = "text/plain";
                    await context.Response.WriteAsync("Forbidden");
                    return;
                }

                // Authentication (and role check, if any) passed - let downstream middleware handle the request
                context.Items["ZauberMediaAuthenticated"] = true;

                // Only this user may have it: no shared cache (CDN, proxy) may keep it and hand it to someone else.
                // Set as the response starts, so the image resizer's own caching headers don't replace it.
                context.Response.OnStarting(() =>
                {
                    context.Response.Headers.CacheControl = "private, no-store";
                    return Task.CompletedTask;
                });
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

        return mediaDict.TryGetValue(Services.MediaService.NormaliseMediaPath(mediaPath), out var entry) ? entry : null;
    }
}
