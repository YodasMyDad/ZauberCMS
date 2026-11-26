using Microsoft.AspNetCore.Http;

namespace ZauberCMS.Core.Security.Interfaces;

/// <summary>
/// A CAPTCHA for the registration page (Cloudflare Turnstile, reCAPTCHA, hCaptcha, …). ZauberCMS ships none:
/// add a public class that implements this to the site's project and it is found by discovery. With more than one
/// in the scanned assemblies, <c>Zauber:Plugins:CaptchaProvider</c> names the one to use. Instances are created
/// with constructor injection.
/// </summary>
public interface ICaptchaProvider
{
    /// <summary>
    /// A component rendered inside the form, just above the submit button. The page is static server-rendered, so
    /// the component outputs plain markup: the widget's element and its script tag. The widget adds its response
    /// to the form as a field of its own.
    /// </summary>
    Type Component { get; }

    /// <summary>
    /// Checks the posted form on <paramref name="httpContext"/> (usually by sending the widget's response field
    /// to the CAPTCHA service). Return false to refuse the registration. An exception refuses it too.
    /// </summary>
    Task<bool> VerifyAsync(HttpContext httpContext, CancellationToken cancellationToken = default);
}
