using Microsoft.AspNetCore.Http;
using ZauberCMS.Core.Security.Models;

namespace ZauberCMS.Core.Security.Interfaces;

/// <summary>
/// Cheap checks that stop most automated form posts without asking the visitor anything: a field people cannot
/// see (scripts fill it in) and the time between showing the form and receiving it (scripts are instant).
/// The registration page uses it; a site can use it on its own static forms the same way: render
/// <see cref="CreateToken"/> in a hidden field named <see cref="TokenFieldName"/>, add an empty text field named
/// <see cref="HoneypotFieldName"/> that is hidden with CSS, and call <see cref="Validate(HttpContext)"/> on the post.
/// Replace the registered service to change the rules.
/// </summary>
public interface IFormProtectionService
{
    /// <summary>The hidden field that carries the token.</summary>
    string TokenFieldName { get; }

    /// <summary>The field that must come back empty.</summary>
    string HoneypotFieldName { get; }

    /// <summary>A token that records when the form was shown. Only this site can produce or read it.</summary>
    string CreateToken();

    /// <summary>Checks the two fields of the posted form on <paramref name="httpContext"/>.</summary>
    FormProtectionResult Validate(HttpContext httpContext);

    /// <summary>Checks a token and a honeypot value taken from a form by other means.</summary>
    FormProtectionResult Validate(string? token, string? honeypotValue);

    /// <summary>
    /// Marks the token as used once the form it came with has been accepted, so the same token cannot send another
    /// form (ZA-150); <see cref="Validate(string?, string?)"/> then returns
    /// <see cref="FormProtectionResult.AlreadyUsed"/> for it. False when it was used already. Validating does not use
    /// a token up: a form shown again after a refused post keeps its token. The default remembers nothing.
    /// </summary>
    bool TryUseToken(string? token) => true;
}
