using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ZauberCMS.Core.Settings;

namespace ZauberCMS.Core.Extensions;

public static class HttpContextExtensions
{
    /// <summary>
    /// Helper to create urls in services and commands/handlers.
    /// The site address is <c>Zauber:SiteUrl</c> when that is set. Otherwise it is taken from the current
    /// request, whose Host header is chosen by whoever sent it: an emailed link built that way can point at
    /// another site, so production sites should set <c>Zauber:SiteUrl</c> (or restrict <c>AllowedHosts</c>).
    /// </summary>
    /// <example>
    /// var url = _httpContextAccessor.ToAbsoluteUrl("/Identity/Account/ConfirmEmail", new { userId = applicationUser.Id, code });
    /// </example>
    /// <param name="httpContextAccessor"></param>
    /// <param name="relativeUrl"></param>
    /// <param name="parameters"></param>
    /// <returns></returns>
    public static string ToAbsoluteUrl(this IHttpContextAccessor httpContextAccessor, string relativeUrl, object? parameters = null)
    {
        var request = httpContextAccessor.HttpContext?.Request;
        var siteUrl = httpContextAccessor.HttpContext?.RequestServices
            .GetService<IOptions<ZauberSettings>>()?.Value.SiteUrl;

        var siteUri = Uri.TryCreate(siteUrl, UriKind.Absolute, out var configured) &&
                      (configured.Scheme == Uri.UriSchemeHttp || configured.Scheme == Uri.UriSchemeHttps)
            ? configured
            : new Uri($"{request?.Scheme}://{request?.Host.Value}");

        var url = new Uri(siteUri, relativeUrl).ToString();

        if (parameters != null)
        {
            url = QueryHelpers.AddQueryString(url, parameters.ToDictionary()!);
        }

        return url;
    }
}