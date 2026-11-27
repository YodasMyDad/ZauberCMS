using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using ZauberCMS.Core.Settings;

namespace ZauberCMS.Core.Media.Middleware;

/// <summary>
/// Adds response headers to everything served from the upload folder. Uploaded files come from users, so the
/// browser must take the declared content type as given (<c>nosniff</c>), and an SVG opened on its own must not be
/// able to run script or load anything from elsewhere (a sandboxing Content-Security-Policy).
/// Runs before the middleware that serves the files (restricted media, image resize, static files).
/// </summary>
internal class MediaSecurityHeadersMiddleware(RequestDelegate next, IOptions<ZauberSettings> settings)
{
    /// <summary>Policy sent with SVG files: no script, no requests, inline styles and embedded bitmaps only.</summary>
    public const string SvgContentSecurityPolicy =
        "default-src 'none'; style-src 'unsafe-inline'; img-src data:; sandbox";

    public Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path;
        if (path.StartsWithSegments($"/{settings.Value.UploadFolderName}", StringComparison.OrdinalIgnoreCase))
        {
            var isSvg = path.Value?.EndsWith(".svg", StringComparison.OrdinalIgnoreCase) == true;
            context.Response.OnStarting(() =>
            {
                var headers = context.Response.Headers;
                headers.XContentTypeOptions = "nosniff";
                if (isSvg)
                {
                    headers.ContentSecurityPolicy = SvgContentSecurityPolicy;
                }

                return Task.CompletedTask;
            });
        }

        return next(context);
    }
}
