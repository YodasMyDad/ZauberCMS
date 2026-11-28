using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using ZauberCMS.Core;
using ZauberCMS.Core.Localization;
using ZauberCMS.Core.Membership;
using ZauberCMS.Core.Membership.Models;

namespace ZauberCMS.Components.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AuthController(
    UserManager<User> userManager,
    SignInManager<User> signInManager,
    IAntiforgery antiforgery,
    RefreshSignInTicketService refreshSignInTickets,
    IOptionsMonitor<CookieAuthenticationOptions> cookieOptions,
    IStringLocalizer<ZauberText> text)
    : ControllerBase
{
    /// <summary>
    /// Re-issues the sign-in cookie after the user changed their own account (user name, email, password, roles).
    /// The cookie is only renewed for a user who may still sign in, and only when the cookie's security stamp is
    /// still valid or the request carries the ticket the change handed out. Anything else ends the session, so a
    /// cookie that was invalidated (password reset, user deleted) cannot be turned into a fresh one (ZA-052).
    /// Not <c>[Authorize]</c>: a cookie older than the security stamp check's interval (five minutes) is refused by
    /// that check before this runs, because the change replaced the stamp; the cookie the browser sent is then read
    /// here, and renewed only together with a ticket for that same user (ZA-218).
    /// </summary>
    [AllowAnonymous]
    [HttpGet("refreshsignin")]
    public async Task<IActionResult> RefreshSignIn(string? redirectUrl = null, string? ticket = null)
    {
        var user = await userManager.GetUserAsync(User);
        if (user != null
            && await signInManager.CanSignInAsync(user)
            && !await userManager.IsLockedOutAsync(user)
            && (await signInManager.ValidateSecurityStampAsync(User) != null
                || refreshSignInTickets.IsValid(ticket, user)))
        {
            await signInManager.RefreshSignInAsync(user);
            return LocalRedirectOrHome(redirectUrl);
        }

        if (user == null)
        {
            var (cookieUser, properties) = await UserOfRefusedCookieAsync();
            if (cookieUser != null
                && refreshSignInTickets.IsValid(ticket, cookieUser)
                && await signInManager.CanSignInAsync(cookieUser)
                && !await userManager.IsLockedOutAsync(cookieUser))
            {
                await signInManager.SignInAsync(cookieUser, properties ?? new AuthenticationProperties());
                return LocalRedirectOrHome(redirectUrl);
            }
        }

        await signInManager.SignOutAsync();
        return Redirect(SitePath(Urls.Account.Login));
    }

    /// <summary>
    /// The user, and the sign-in's properties ("remember me"), of the application cookie this request carries when
    /// the cookie handler did not accept it: still within its lifetime, and protected by this site's keys.
    /// </summary>
    private async Task<(User? User, AuthenticationProperties? Properties)> UserOfRefusedCookieAsync()
    {
        var options = cookieOptions.Get(IdentityConstants.ApplicationScheme);
        var cookie = options.Cookie.Name is { } name ? options.CookieManager.GetRequestCookie(HttpContext, name) : null;
        var cookieTicket = string.IsNullOrEmpty(cookie) ? null : options.TicketDataFormat.Unprotect(cookie);
        if (cookieTicket?.Principal is not { } principal ||
            cookieTicket.Properties.ExpiresUtc is { } expires && expires <= DateTimeOffset.UtcNow)
        {
            return (null, null);
        }

        var cookieUser = await userManager.GetUserAsync(principal);
        return cookieUser is { Deleted: false } ? (cookieUser, cookieTicket.Properties) : (null, null);
    }

    // GET renders a tiny form that posts the sign-out (the POST below checks the antiforgery token). It submits
    // itself only when the browser says the navigation came from this site or was typed in: a link or redirect
    // from another site shows a "Sign out" button instead, so other sites cannot sign visitors out (ZA-030). A bare
    // <img src="...logout"> only fetches this HTML; scripts do not run in image fetches.
    [HttpGet("logout")]
    public ContentResult LogoutGet(string? redirectUrl = null)
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        var safeRedirect = !string.IsNullOrEmpty(redirectUrl) && Url.IsLocalUrl(redirectUrl) ? redirectUrl : SitePath("/");
        var enc = HtmlEncoder.Default;
        var autoSubmit = IsFromThisSite(Request);
        // In the request's culture (Roadmap D95); encoded, since a translation is text, not markup
        var signOut = enc.Encode(text["account_signOut"].Value);
        var html =
            "<!doctype html><html><head><meta charset=\"utf-8\"><meta name=\"robots\" content=\"noindex\">" +
            $"<title>{signOut}</title></head><body>" +
            $"<form id=\"zauber-logout\" method=\"post\" action=\"{enc.Encode(SitePath(Urls.ApiLogout))}\">" +
            $"<input type=\"hidden\" name=\"{enc.Encode(tokens.FormFieldName)}\" value=\"{enc.Encode(tokens.RequestToken!)}\" />" +
            $"<input type=\"hidden\" name=\"redirectUrl\" value=\"{enc.Encode(safeRedirect)}\" />" +
            (autoSubmit
                ? $"<noscript><button type=\"submit\">{signOut}</button></noscript>"
                : $"<p>{enc.Encode(text["account_signOutConfirm"].Value)}</p><button type=\"submit\">{signOut}</button>") +
            "</form>" +
            (autoSubmit ? "<script>document.getElementById('zauber-logout').submit();</script>" : string.Empty) +
            "</body></html>";
        Response.Headers.CacheControl = "no-store";
        // Another site may not show this page in a frame and trick a click on its button
        Response.Headers.XFrameOptions = "DENY";
        Response.Headers.ContentSecurityPolicy = "frame-ancestors 'none'";
        return Content(html, "text/html");
    }

    /// <summary>
    /// True when the browser says the request came from a page of this site, or from the address bar or a
    /// bookmark (<c>Sec-Fetch-Site</c>). A browser that does not send it must send a Referer on this host; without
    /// either the visitor gets the button (a page can hide its Referer).
    /// </summary>
    internal static bool IsFromThisSite(HttpRequest request)
    {
        var fetchSite = request.Headers["Sec-Fetch-Site"].ToString();
        if (!string.IsNullOrEmpty(fetchSite))
        {
            return fetchSite is "same-origin" or "none";
        }

        var referer = request.Headers.Referer.ToString();
        return Uri.TryCreate(referer, UriKind.Absolute, out var from) &&
               string.Equals(from.Authority, request.Host.Value, StringComparison.OrdinalIgnoreCase);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromForm] string? redirectUrl = null)
    {
        // The antiforgery token is checked here, 400 without a valid one. [ValidateAntiForgeryToken] would do the same
        // (MVC's view features are registered, ZA-185); this check does not depend on them.
        if (!await antiforgery.IsRequestValidAsync(HttpContext))
        {
            return BadRequest();
        }

        await signInManager.SignOutAsync();
        return LocalRedirectOrHome(redirectUrl);
    }

    // Defence against open-redirect: only honour same-site redirect targets (a path from the host's root, path base
    // included, as a return address is), fall back to the site's home page otherwise.
    private RedirectResult LocalRedirectOrHome(string? redirectUrl)
    {
        if (!string.IsNullOrEmpty(redirectUrl) && Url.IsLocalUrl(redirectUrl))
        {
            return Redirect(redirectUrl);
        }
        return Redirect(SitePath("/"));
    }

    // A path on this site as the browser needs it: the request's path base, then the path (ZA-219)
    private string SitePath(string path) => Request.PathBase.Add(new PathString(path)).ToString();
}
