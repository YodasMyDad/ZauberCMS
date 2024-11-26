using System.Globalization;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using ZauberCMS.Core.Content.Commands;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Languages.Middleware;

public class SetCultureMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IMediator mediator, RequestCulture requestCulture)
    {
        // Get the list of domains and content languages
        var domains = await mediator.Send(new CachedDomainsCommand());
        var contentWithLanguages = await mediator.Send(new GetContentLanguagesCommand());

        var fullUrl = $"{context.Request.Scheme}://{context.Request.Host}{context.Request.Path}";
        var slug = context.Request.Path.HasValue ? context.Request.Path.Value.Trim('/').ToLowerInvariant() : string.Empty;

        // Get the file extension if it exists
        var fileExtension = Path.GetExtension(slug);
        
        /*if (string.IsNullOrEmpty(fileExtension))
        {*/
            // Match the domain
            var matchedDomain = MatchDomainWithContent(fullUrl, domains);

            string? languageIsoCode = null;

            // Use the matched domain's language if available
            if (matchedDomain?.Language?.LanguageIsoCode != null)
            {
                languageIsoCode = matchedDomain.Language.LanguageIsoCode;
            }
            else if (contentWithLanguages.TryGetValue(slug, out var contentLanguage)) // Use slug to find language
            {
                languageIsoCode = contentLanguage;
            }
        
            // Set the culture based on the resolved language
            if (!languageIsoCode.IsNullOrWhiteSpace())
            {
                var cultureInfo = new CultureInfo(languageIsoCode);
                CultureInfo.CurrentCulture = cultureInfo;
                CultureInfo.CurrentUICulture = cultureInfo;
                CultureInfo.DefaultThreadCurrentCulture = cultureInfo;
                CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;
                requestCulture.LanguageIsoCode = languageIsoCode;
            }
            /*else if (content != null)
            {
                foreach (var guid in content.Path)
                {
                    if (contentWithLanguages.TryGetValue(guid, out var language))
                    {
                        var cultureInfo = new CultureInfo(language);
                        CultureInfo.CurrentCulture = cultureInfo;
                        CultureInfo.CurrentUICulture = cultureInfo;
                        break;
                    }
                }
            }*/
    
        
        await next(context);
    }

    private static Domain? MatchDomainWithContent(string url, List<Domain> domains)
    {
        var uri = new Uri(url);
        var requestHost = uri.Host.ToLower();
        var requestPath = uri.AbsolutePath.TrimStart('/').ToLower();

        return domains.FirstOrDefault(domain =>
        {
            var domainUrl = domain.Url?.ToLower();
            if (domainUrl != null && domainUrl.Contains('/'))
            {
                var parts = domainUrl.Split('/', 2);
                var domainHost = parts[0];
                var domainPath = parts.Length > 1 ? parts[1] : string.Empty;

                return requestHost == domainHost && requestPath.StartsWith(domainPath);
            }

            return requestHost == domainUrl;
        });
    }
}

public static class CustomCultureMiddlewareExtensions
{
    public static IApplicationBuilder UseCustomCulture(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<SetCultureMiddleware>();
    }
}