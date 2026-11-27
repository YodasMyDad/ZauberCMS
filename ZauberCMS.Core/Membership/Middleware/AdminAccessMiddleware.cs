using System.Net;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using ZauberCMS.Core.Data.Interfaces;
using ZauberCMS.Core.Extensions;

namespace ZauberCMS.Core.Membership.Middleware;

/// <summary>
/// Guards every request under <c>/admin</c>, wherever the page comes from (this package, the site, a plugin).
/// <list type="number">
/// <item>The admin IP allow-list (<c>GlobalSettings.AllowedAdminIpAddress</c>): when it has entries, a request from
/// any other address gets 403.</item>
/// <item>Fail-closed authorisation: a page that declares no authorisation of its own is treated as
/// <c>[Authorize(Roles = "Admin")]</c>. A page that carries <c>[Authorize]</c> (any roles or policy) or
/// <c>[AllowAnonymous]</c> made its own choice and is left to the normal authorisation middleware.</item>
/// </list>
/// Must run after authentication and routing, and before authorisation.
/// </summary>
public class AdminAccessMiddleware(RequestDelegate next, ILogger<AdminAccessMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, IDataService dataService)
    {
        if (!AdminAccess.IsAdminPath(context.Request.Path))
        {
            await next(context);
            return;
        }

        // Arriving from a static page by enhanced navigation would patch the admin into that page, and the
        // admin's scripts (Radzen, Sortable) would not run (ZA-096). Without this header blazor.web.js loads the
        // address as a full page instead, whichever link, redirect or form led here.
        context.Response.OnStarting(static state =>
        {
            ((HttpResponse)state).Headers.Remove(AdminAccess.EnhancedNavigationHeader);
            return Task.CompletedTask;
        }, context.Response);

        var globalSettings = await dataService.GetGlobalSettings();
        if (!AdminAccess.IsAllowedAddress(context.Connection.RemoteIpAddress, globalSettings.AllowedAdminIpAddress))
        {
            logger.LogWarning("Admin request from {RemoteIp} refused: not in the allowed admin IP addresses",
                context.Connection.RemoteIpAddress);
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        var endpoint = context.GetEndpoint();
        if (endpoint != null && !AdminAccess.DeclaresAuthorization(endpoint.Metadata))
        {
            if (context.User.Identity?.IsAuthenticated != true)
            {
                await context.ChallengeAsync();
                return;
            }

            if (!context.User.IsInRole(Constants.Roles.AdminRoleName))
            {
                await context.ForbidAsync();
                return;
            }
        }

        await next(context);
    }
}

/// <summary>The rules <see cref="AdminAccessMiddleware"/> and the admin router apply.</summary>
public static class AdminAccess
{
    /// <summary>
    /// The response header with which a Blazor endpoint accepts an enhanced navigation (<c>allow</c>). Admin responses
    /// go without it, so the browser loads the admin as a full page (ZA-096).
    /// </summary>
    public const string EnhancedNavigationHeader = "blazor-enhanced-nav";

    /// <summary>True for <c>/admin</c> and everything below it (any letter case).</summary>
    public static bool IsAdminPath(PathString path) =>
        path.StartsWithSegments(Urls.AdminBaseUrl, StringComparison.OrdinalIgnoreCase);

    /// <summary>True for an address or path that points at <c>/admin</c> or below.</summary>
    public static bool IsAdminPath(string? pathOrUrl)
    {
        if (string.IsNullOrWhiteSpace(pathOrUrl))
        {
            return false;
        }

        var path = Uri.TryCreate(pathOrUrl, UriKind.Absolute, out var absolute) && !absolute.IsFile
            ? absolute.AbsolutePath
            : pathOrUrl.Split('?', '#')[0];
        return IsAdminPath(new PathString("/" + path.TrimStart('/')));
    }

    /// <summary>True when the page, component or endpoint carries its own <c>[Authorize]</c> or <c>[AllowAnonymous]</c>.</summary>
    public static bool DeclaresAuthorization(IEnumerable<object> metadata) =>
        metadata.Any(item => item is IAuthorizeData or IAllowAnonymous);

    /// <summary>True when the type (a routable component) carries its own <c>[Authorize]</c> or <c>[AllowAnonymous]</c>.</summary>
    public static bool DeclaresAuthorization(Type pageType) =>
        DeclaresAuthorization(pageType.GetCustomAttributes(inherit: true));

    /// <summary>
    /// The admin IP allow-list. An empty list allows everyone. With entries, the remote address must equal one of
    /// them; an unknown remote address is refused. Entries that are not IP addresses match nothing.
    /// </summary>
    public static bool IsAllowedAddress(IPAddress? remoteAddress, IReadOnlyCollection<string> allowedAddresses)
    {
        var entries = allowedAddresses.Where(entry => !string.IsNullOrWhiteSpace(entry)).ToList();
        if (entries.Count == 0)
        {
            return true;
        }

        if (remoteAddress == null)
        {
            return false;
        }

        var remote = Normalise(remoteAddress);
        return entries.Any(entry => IPAddress.TryParse(entry.Trim(), out var allowed) && Normalise(allowed).Equals(remote));
    }

    private static IPAddress Normalise(IPAddress address) =>
        address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
}
