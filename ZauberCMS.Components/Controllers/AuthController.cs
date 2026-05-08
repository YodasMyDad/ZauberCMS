using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ZauberCMS.Core.Membership.Models;

namespace ZauberCMS.Components.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AuthController(
    UserManager<User> userManager,
    SignInManager<User> signInManager,
    IAntiforgery antiforgery)
    : ControllerBase
{
    [HttpGet("refreshsignin")]
    public async Task<IActionResult> RefreshSignIn(string? redirectUrl = null)
    {
        var user = await userManager.GetUserAsync(User);
        if (user != null) await signInManager.RefreshSignInAsync(user);
        return LocalRedirectOrHome(redirectUrl);
    }

    // GET renders a tiny auto-submitting form. A bare <img src="...logout"> only fetches
    // this HTML — JS does not execute in image-fetch contexts, so the actual sign-out
    // (the POST below) is never triggered. Any genuine browser navigation auto-posts
    // through here within milliseconds.
    [HttpGet("logout")]
    public ContentResult LogoutGet(string? redirectUrl = null)
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        var safeRedirect = !string.IsNullOrEmpty(redirectUrl) && Url.IsLocalUrl(redirectUrl) ? redirectUrl : "/";
        var enc = HtmlEncoder.Default;
        var html =
            "<!doctype html><html><head><meta charset=\"utf-8\"><title>Signing out…</title></head><body>" +
            "<form id=\"zauber-logout\" method=\"post\" action=\"/api/auth/logout\">" +
            $"<input type=\"hidden\" name=\"{enc.Encode(tokens.FormFieldName)}\" value=\"{enc.Encode(tokens.RequestToken!)}\" />" +
            $"<input type=\"hidden\" name=\"redirectUrl\" value=\"{enc.Encode(safeRedirect)}\" />" +
            "<noscript><button type=\"submit\">Click here to log out</button></noscript>" +
            "</form>" +
            "<script>document.getElementById('zauber-logout').submit();</script>" +
            "</body></html>";
        return Content(html, "text/html");
    }

    [HttpPost("logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout([FromForm] string? redirectUrl = null)
    {
        await signInManager.SignOutAsync();
        return LocalRedirectOrHome(redirectUrl);
    }

    // Defence against open-redirect: only honour same-site redirect targets, fall back to "/" otherwise.
    private IActionResult LocalRedirectOrHome(string? redirectUrl)
    {
        if (!string.IsNullOrEmpty(redirectUrl) && Url.IsLocalUrl(redirectUrl))
        {
            return Redirect(redirectUrl);
        }
        return Redirect("/");
    }
}
