using Microsoft.AspNetCore.Http;
using ZauberCMS.Core.Membership.Middleware;

namespace ZauberCMS.Core.Middleware;

/// <summary>
/// The paths ZauberCMS keeps for itself, matched on whole path segments: <c>/admin</c> and <c>/admin/content</c>
/// are the admin, <c>/administration</c> is a page of the site (ZA-095).
/// </summary>
public static class SitePaths
{
    /// <summary>The account pages (sign in, register, password reset).</summary>
    public const string Account = "/account";

    /// <summary>The site's API endpoints.</summary>
    public const string Api = "/api";

    /// <summary>Blazor's connection for interactive components.</summary>
    public const string Blazor = "/_blazor";

    /// <summary>
    /// True for the admin, the account pages, <c>/api</c> and Blazor's <c>/_blazor</c>, and everything below them
    /// (any letter case). These are never content: the culture and redirect middleware leave them alone.
    /// </summary>
    public static bool IsSystemPath(PathString path) =>
        AdminAccess.IsAdminPath(path) ||
        path.StartsWithSegments(Account, StringComparison.OrdinalIgnoreCase) ||
        path.StartsWithSegments(Api, StringComparison.OrdinalIgnoreCase) ||
        path.StartsWithSegments(Blazor, StringComparison.OrdinalIgnoreCase);
}
