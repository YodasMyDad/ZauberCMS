using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

namespace ZauberCMS.Core.Middleware;

/// <summary>
/// A site under a path base reads its own cookies, not same-named ones of another site on the host (ZA-252, Roadmap
/// D143). ASP.NET Core writes a site's sign-in, external sign-in and antiforgery cookies at the site's path base
/// (<c>/site</c>), and a browser sends the cookies of the longer path first (RFC 6265 §5.4), so a request to
/// <c>/site/…</c> carries the site's <c>.AspNetCore.Identity.Application</c> before one of the same name at <c>/</c>
/// (a site at the host's root, or this one before it moved). ASP.NET Core reads the last cookie of a name, which is
/// that other one: the sign-in did not hold and a form post was refused (400) until the browser's cookies were
/// cleared. Under a path base the request reads the first cookie of each name instead; at the host's root nothing
/// changes (the site's cookies are at <c>/</c> there, the shortest path, and the framework reads them as before).
/// A cookies feature a site set before <c>AddZauberCms</c> (any type but the framework's own
/// <see cref="RequestCookiesFeature"/>) is kept, which is also how a site turns this rule off (ZA-294).
/// </summary>
internal sealed class PathBaseCookiesMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        if (context.Request.PathBase.HasValue && IsTheFrameworks(context.Features.Get<IRequestCookiesFeature>()))
        {
            context.Features.Set<IRequestCookiesFeature>(new FirstCookieOfEachNameFeature(context.Features));
        }

        return next(context);
    }

    // None yet (the framework makes its own on the first read of Request.Cookies), or that one; anything else is the
    // site's own
    private static bool IsTheFrameworks(IRequestCookiesFeature? feature) =>
        feature == null || feature.GetType() == typeof(RequestCookiesFeature);

    /// <summary>
    /// The request's cookies with the first value of each name. The header is parsed by ASP.NET Core's own parser,
    /// so names, quoting and unescaping are the framework's; only a repeated name reads differently. The request's
    /// Cookie header is left as the browser sent it.
    /// </summary>
    internal sealed class FirstCookieOfEachNameFeature(IFeatureCollection features) : IRequestCookiesFeature
    {
        private readonly RequestCookiesFeature _framework = new(features);
        private StringValues _readFrom;
        private IRequestCookieCollection? _cookies;

        public IRequestCookieCollection Cookies
        {
            get
            {
                // Read again when the header changes, as the framework's feature does
                var header = features.GetRequiredFeature<IHttpRequestFeature>().Headers.Cookie;
                if (_cookies == null || !StringValues.Equals(header, _readFrom))
                {
                    _readFrom = header;
                    _cookies = FirstOfEachName(header) is { } firstOfEachName
                        ? Parse(firstOfEachName)
                        : _framework.Cookies;
                }

                return _cookies;
            }
            set
            {
                // The framework's setter writes the Cookie header from the collection; the getter reads that again
                _framework.Cookies = value;
                _cookies = null;
            }
        }

        /// <summary>The header with only the first cookie of each name, or null when no name repeats.</summary>
        private static string? FirstOfEachName(StringValues header)
        {
            if (header.Count == 0 || !CookieHeaderValue.TryParseList(header, out var cookies))
            {
                return null;
            }

            // Names compare as the framework's cookie collection compares them (ignoring case)
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var firstOfEachName = cookies.Where(cookie => names.Add(cookie.Name.ToString())).ToList();
            return firstOfEachName.Count == cookies.Count
                ? null
                : string.Join("; ", firstOfEachName.Select(cookie => cookie.ToString()));
        }

        private static IRequestCookieCollection Parse(string header)
        {
            var request = new FeatureCollection();
            request.Set<IHttpRequestFeature>(new HttpRequestFeature
            {
                Headers = new HeaderDictionary { [HeaderNames.Cookie] = header }
            });
            return new RequestCookiesFeature(request).Cookies;
        }
    }
}
