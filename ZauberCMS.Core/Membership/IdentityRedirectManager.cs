using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Membership;

public sealed class IdentityRedirectManager(NavigationManager navigationManager)
{
    public const string StatusCookieName = "Identity.StatusMessage";

    // At the site's path base, as ASP.NET Core writes its own cookies, not at "/": there it reached every site on the
    // host (ZA-290). StatusMessage deletes it at the same path.
    private static readonly CookieBuilder StatusCookieBuilder = new Microsoft.AspNetCore.Authentication.RequestPathBaseCookieBuilder
    {
        SameSite = SameSiteMode.Strict,
        HttpOnly = true,
        IsEssential = true,
        MaxAge = TimeSpan.FromSeconds(5),
    };

    [DoesNotReturn]
    public void RedirectTo(string? uri)
    {
        uri ??= "";

        // Defence-in-depth: protocol-relative URLs ("//host", "/\host") slip past IsWellFormedUriString as "well-formed relative" but the browser treats them as off-host redirects.
        if (IsProtocolRelative(uri))
        {
            uri = "/";
        }
        else if (!Uri.IsWellFormedUriString(uri, UriKind.Relative))
        {
            // An absolute address below this site's base; one on another site, or one that cannot be read, goes to
            // the home page instead of failing the request after the sign-in (ZA-253)
            try
            {
                uri = navigationManager.ToBaseRelativePath(uri);
            }
            catch (ArgumentException)
            {
                uri = "";
            }
        }

        // During static rendering, NavigateTo throws a NavigationException which is handled by the framework as a redirect.
        // So as long as this is called from a statically rendered Identity component, the InvalidOperationException is never thrown.
        navigationManager.NavigateTo(uri);
        throw new InvalidOperationException(
            $"{nameof(IdentityRedirectManager)} can only be used during static rendering.");
    }

    private static bool IsProtocolRelative(string url)
        => url.Length >= 2 && url[0] == '/' && (url[1] == '/' || url[1] == '\\');

    [DoesNotReturn]
    public void RedirectTo(string uri, Dictionary<string, object?> queryParameters)
    {
        var uriWithoutQuery = navigationManager.ToAbsoluteUri(uri).GetLeftPart(UriPartial.Path);
        var newUri = navigationManager.GetUriWithQueryParameters(uriWithoutQuery, queryParameters);
        RedirectTo(newUri);
    }

    /// <summary>The cookie that carries the status message's <see cref="ResultMessageType"/> beside it.</summary>
    public const string StatusTypeCookieName = "Identity.StatusMessageType";

    /// <summary>
    /// Redirects and shows <paramref name="message"/> on the page it lands on (the account pages'
    /// <c>StatusMessage</c>): as an error when it starts with "Error", as in ASP.NET's Identity template, otherwise as
    /// a success. A localised message cannot count on its first word: use the overload with a type.
    /// </summary>
    [DoesNotReturn]
    public void RedirectToWithStatus(string uri, string message, HttpContext context)
    {
        context.Response.Cookies.Append(StatusCookieName, message, StatusCookieBuilder.Build(context));
        RedirectTo(uri);
    }

    /// <summary>
    /// Redirects and shows <paramref name="message"/> as a message of <paramref name="type"/>, whatever its language
    /// (ZA-280).
    /// </summary>
    [DoesNotReturn]
    public void RedirectToWithStatus(string uri, string message, ResultMessageType type, HttpContext context)
    {
        context.Response.Cookies.Append(StatusTypeCookieName, type.ToString(), StatusCookieBuilder.Build(context));
        RedirectToWithStatus(uri, message, context);
    }

    private string CurrentPath => navigationManager.ToAbsoluteUri(navigationManager.Uri).GetLeftPart(UriPartial.Path);

    [DoesNotReturn]
    public void RedirectToCurrentPage() => RedirectTo(CurrentPath);

    [DoesNotReturn]
    public void RedirectToCurrentPageWithStatus(string message, HttpContext context)
        => RedirectToWithStatus(CurrentPath, message, context);
}