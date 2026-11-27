using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ZauberCMS.Core;
using ZauberCMS.Core.Membership;
using ZauberCMS.Core.Membership.Models;

namespace ZauberCMS.Components.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AuthController(
    UserManager<User> userManager,
    SignInManager<User> signInManager,
    IAntiforgery antiforgery,
    RefreshSignInTicketService refreshSignInTickets)
    : ControllerBase
{
    /// <summary>
    /// Re-issues the sign-in cookie after the user changed their own account (user name, email, roles).
    /// The cookie is only renewed for a user who may still sign in, and only when the cookie's security stamp is
    /// still valid or the request carries the ticket the save handed out. Anything else ends the session, so a
    /// cookie that was invalidated (password reset, user deleted) cannot be turned into a fresh one (ZA-052).
    /// </summary>
    [Authorize]
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

        await signInManager.SignOutAsync();
        return Redirect(Urls.Account.Login);
    }

    // GET renders a tiny form that posts the sign-out (the POST below checks the antiforgery token). It submits
    // itself only when the browser says the navigation came from this site or was typed in: a link or redirect
    // from another site shows a "Sign out" button instead, so other sites cannot sign visitors out (ZA-030). A bare
    // <img src="...logout"> only fetches this HTML; scripts do not run in image fetches.
    [HttpGet("logout")]
    public ContentResult LogoutGet(string? redirectUrl = null)
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        var safeRedirect = !string.IsNullOrEmpty(redirectUrl) && Url.IsLocalUrl(redirectUrl) ? redirectUrl : "/";
        var enc = HtmlEncoder.Default;
        var autoSubmit = IsFromThisSite(Request);
        var html =
            "<!doctype html><html><head><meta charset=\"utf-8\"><meta name=\"robots\" content=\"noindex\">" +
            "<title>Sign out</title></head><body>" +
            "<form id=\"zauber-logout\" method=\"post\" action=\"/api/auth/logout\">" +
            $"<input type=\"hidden\" name=\"{enc.Encode(tokens.FormFieldName)}\" value=\"{enc.Encode(tokens.RequestToken!)}\" />" +
            $"<input type=\"hidden\" name=\"redirectUrl\" value=\"{enc.Encode(safeRedirect)}\" />" +
            (autoSubmit
                ? "<noscript><button type=\"submit\">Sign out</button></noscript>"
                : "<p>Do you want to sign out?</p><button type=\"submit\">Sign out</button>") +
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

    // Defence against open-redirect: only honour same-site redirect targets, fall back to "/" otherwise.
    private RedirectResult LocalRedirectOrHome(string? redirectUrl)
    {
        if (!string.IsNullOrEmpty(redirectUrl) && Url.IsLocalUrl(redirectUrl))
        {
            return Redirect(redirectUrl);
        }
        return Redirect("/");
    }
}
