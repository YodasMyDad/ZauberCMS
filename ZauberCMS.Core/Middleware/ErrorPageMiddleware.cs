using System.Diagnostics;
using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace ZauberCMS.Core.Middleware;

/// <summary>
/// The last resort behind the site's error page (ZA-098, Roadmap D33). Outside Development setup sends an unhandled
/// exception to <c>Zauber:ErrorPagePath</c> (ASP.NET's exception handler, just inside this middleware). When that
/// page is missing or fails as well (the database is down, the layout throws), the exception handler gives the
/// original exception back and this middleware answers with a plain page that needs nothing: no database, no
/// layout, no component. It shows the request id, which is in the site's log next to the exception.
/// </summary>
internal class ErrorPageMiddleware(RequestDelegate next, ILogger<ErrorPageMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex) when (!context.Response.HasStarted && !context.RequestAborted.IsCancellationRequested)
        {
            // The exception handler has logged the exception; this only records that its page could not be shown
            logger.LogError(ex, "The error page could not be shown for {Path}; the built-in page was sent instead",
                context.Request.Path);
            await WriteAsync(context);
        }
    }

    /// <summary>Sends the built-in error page: status 500, not cached, the request id.</summary>
    public static Task WriteAsync(HttpContext context)
    {
        context.Response.Clear();
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.Headers.CacheControl = "no-store";
        if (HttpMethods.IsHead(context.Request.Method))
        {
            return Task.CompletedTask;
        }

        var requestId = WebUtility.HtmlEncode(Activity.Current?.Id ?? context.TraceIdentifier);
        return context.Response.WriteAsync(
            $$"""
              <!DOCTYPE html>
              <html lang="en">
              <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <meta name="robots" content="noindex">
              <title>Something went wrong</title>
              <style>
              body { font-family: system-ui, sans-serif; color: #1f2937; background: #f9fafb; margin: 0; }
              main { max-width: 36rem; margin: 15vh auto 0; padding: 0 1.5rem; }
              h1 { font-size: 1.5rem; margin: 0 0 1rem; }
              p { line-height: 1.5; }
              code { font-size: .85rem; color: #6b7280; }
              </style>
              </head>
              <body>
              <main>
              <h1>Something went wrong</h1>
              <p>This page could not be shown. Please try again in a moment.</p>
              <p><code>Request id: {{requestId}}</code></p>
              </main>
              </body>
              </html>
              """);
    }
}
