using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;

namespace ZauberCMS.Core.Extensions;

public static class HttpContextExtensions
{
    /// <summary>
    /// Helper to create urls in services and commands/handlers
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

        var url = new Uri(new Uri($"{request?.Scheme}://{request?.Host.Value}"), relativeUrl).ToString();

        if (parameters != null)
        {
            url = QueryHelpers.AddQueryString(url, parameters.ToDictionary()!);
        }

        return url;
    }
}