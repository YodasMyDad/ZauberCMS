using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using ZauberCMS.Core.Data.Interfaces;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Sections.Authorization;
using ZauberCMS.Core.Sections.Interfaces;

namespace ZauberCMS.Core.Membership.Middleware;

/// <summary>
/// Guards every request under <c>/admin</c>, wherever the page comes from (this package, the site, a plugin).
/// <list type="number">
/// <item>The admin IP allow-list (<c>GlobalSettings.AllowedAdminIpAddress</c>): when it has entries, a request from
/// any other address gets 403.</item>
/// <item>Fail-closed authorisation: a page that declares no authorisation of its own is for the users who may open
/// the section its address is under (<see cref="IBackOfficeAccessService.GetSectionForPath"/>, Roadmap D67), and for
/// administrators only when it is under no section. A page that carries <c>[Authorize]</c> (any roles or policy,
/// <c>[AuthorizeSection]</c> included) or <c>[AllowAnonymous]</c> made its own choice and is left to the normal
/// authorisation middleware.</item>
/// </list>
/// Must run after authentication and routing, and before authorisation.
/// </summary>
internal class AdminAccessMiddleware(RequestDelegate next, ILogger<AdminAccessMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, IDataService dataService, IBackOfficeAccessService backOfficeAccess)
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

        // /admin itself: straight to the first section the user may open (Roadmap D67), so the landing page does not
        // load the admin once only to navigate away from it. Nobody signed in, or with no section, goes on to the
        // landing page, whose [AuthorizeSection] sends them to sign in or refuses them.
        if (HttpMethods.IsGet(context.Request.Method) && AdminAccess.IsAdminRoot(context.Request.Path) &&
            context.User.Identity?.IsAuthenticated == true)
        {
            var sections = await backOfficeAccess.GetSectionsAsync(context.User, context.RequestAborted);
            var first = sections.Count > 0 ? sections[0] : null;
            var target = string.IsNullOrWhiteSpace(first?.IndexUrl) ? null : "/" + first.IndexUrl.Trim().TrimStart('/');
            if (target != null && !AdminAccess.IsAdminRoot(new PathString(target)))
            {
                context.Response.Redirect(context.Request.PathBase + target);
                return;
            }
        }

        var endpoint = context.GetEndpoint();
        if (endpoint != null && !AdminAccess.DeclaresAuthorization(endpoint.Metadata))
        {
            if (context.User.Identity?.IsAuthenticated != true)
            {
                await context.ChallengeAsync();
                return;
            }

            if (!await AdminAccess.MayOpenUndeclaredPageAsync(context.User, context.Request.Path.Value, backOfficeAccess,
                    context.RequestAborted))
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

    /// <summary>True for <c>/admin</c> itself (with or without the closing slash, any letter case), the admin's landing
    /// address.</summary>
    public static bool IsAdminRoot(PathString path) =>
        path.Equals(Urls.AdminBaseUrl, StringComparison.OrdinalIgnoreCase) ||
        path.Equals(Urls.AdminBaseUrl + "/", StringComparison.OrdinalIgnoreCase);

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

    /// <summary>
    /// The path of the current address below the site's path base, starting with <c>/</c>, without query or fragment.
    /// <c>NavigationManager.Uri</c> is absolute and starts with the path base, so under one (<c>UsePathBase</c>) a test
    /// of its path for <c>/admin</c> never matched (ZA-199).
    /// </summary>
    public static string GetBaseRelativePath(NavigationManager navigationManager) =>
        "/" + navigationManager.ToBaseRelativePath(navigationManager.Uri).Split('?', '#')[0];

    /// <summary>True when the current address is <c>/admin</c> or below, under the site's path base too (ZA-199).</summary>
    public static bool IsAdminPath(NavigationManager navigationManager) =>
        IsAdminPath(GetBaseRelativePath(navigationManager));

    /// <summary>
    /// Who may open a page under <c>/admin</c> that declares no authorisation of its own (Roadmap D67): an
    /// administrator always; anyone else when the address is under a section they may open
    /// (<see cref="IBackOfficeAccessService.GetSectionForPath"/>). <paramref name="path"/> is relative to the path base.
    /// </summary>
    public static async Task<bool> MayOpenUndeclaredPageAsync(ClaimsPrincipal user, string? path,
        IBackOfficeAccessService backOfficeAccess, CancellationToken cancellationToken = default)
    {
        if (user.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        if (user.IsInRole(Constants.Roles.AdminRoleName))
        {
            return true;
        }

        var section = backOfficeAccess.GetSectionForPath(path);
        return section != null && await backOfficeAccess.CanOpenSectionAsync(user, section.Alias, cancellationToken);
    }

    /// <summary>
    /// The authorisation policy for a page under <c>/admin</c> that declares none of its own, for Blazor's
    /// <c>AuthorizeView</c>: the policy of the section the address is under, or null when it is under no section (then
    /// the page is for administrators only). <paramref name="path"/> is relative to the path base.
    /// </summary>
    public static string? UndeclaredPagePolicy(string? path, IBackOfficeAccessService backOfficeAccess) =>
        backOfficeAccess.GetSectionForPath(path) is { } section ? SectionPolicies.ForSections(section.Alias) : null;

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
