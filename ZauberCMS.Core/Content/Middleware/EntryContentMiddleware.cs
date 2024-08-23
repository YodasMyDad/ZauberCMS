using System.Globalization;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.DynamicLinq;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Content.Commands;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Shared.Services;

namespace ZauberCMS.Core.Content.Middleware;

public class EntryContentMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, RequestDataService requestDataService,
        IServiceProvider serviceProvider, IMediator mediator)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ZauberDbContext>();

        var domains = await mediator.Send(new CachedDomainsCommand());
        var contentWithLanguages = await mediator.Send(new GetContentLanguagesCommand());

        var fullUrl = $"{context.Request.Scheme}://{context.Request.Host}{context.Request.Path}";
        var slug = context.Request.Path.HasValue ? context.Request.Path.Value.Trim('/') : string.Empty;
        var isRootContent = slug.IsNullOrWhiteSpace();

        var contentQueryable = dbContext.Contents
            .AsNoTracking()
            .Include(x => x.ContentType)
            .Include(x => x.Language);

        Domain? matchedDomain = null;
        if (!context.Request.Path.HasValue && domains.Count != 0)
        {
            matchedDomain = MatchDomainWithContent(fullUrl, domains);
        }

        // If this is root content, get the first one with minimal data
        var content = isRootContent
            ? matchedDomain != null
                ? await contentQueryable
                    .Select(c => new { c.Id, c.InternalRedirectId, c.ContentType!.IncludeChildren, c.Language, c.Path })
                    .FirstOrDefaultAsync(x => x.Id == matchedDomain.ContentId)
                : await contentQueryable
                    .Where(c => c.IsRootContent && c.Published)
                    .Select(c => new { c.Id, c.InternalRedirectId, c.ContentType!.IncludeChildren, c.Language, c.Path })
                    .FirstOrDefaultAsync()
            : await contentQueryable
                .Where(c => c.Url == slug && c.Published)
                .Select(c => new { c.Id, c.InternalRedirectId, c.ContentType!.IncludeChildren, c.Language, c.Path })
                .FirstOrDefaultAsync();

        // If this content has an internal redirect id, get that content's ID instead
        if (content?.InternalRedirectId != null && content.InternalRedirectId != Guid.Empty)
        {
            var internalRedirectIdValue = content.InternalRedirectId.Value;
            content = await contentQueryable
                .Where(c => c.Id == internalRedirectIdValue)
                .Select(c => new { c.Id, c.InternalRedirectId, c.ContentType!.IncludeChildren, c.Language, c.Path })
                .FirstOrDefaultAsync();
        }

        // Should Domain override language set on content? Or should this be the other way around?
        if (matchedDomain?.Language?.LanguageIsoCode != null)
        {
            /*var cultureInfo = new CultureInfo(matchedDomain.Language.LanguageIsoCode);
            CultureInfo.DefaultThreadCurrentCulture = cultureInfo;
            CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;*/
            var cultureInfo = new CultureInfo(matchedDomain.Language.LanguageIsoCode);
            CultureInfo.CurrentCulture = cultureInfo;
            CultureInfo.CurrentUICulture = cultureInfo;
        }
        else
            // Set the language
        if (content?.Language?.LanguageIsoCode != null)
        {
            var cultureInfo = new CultureInfo(content.Language.LanguageIsoCode);
            CultureInfo.CurrentCulture = cultureInfo;
            CultureInfo.CurrentUICulture = cultureInfo;
        }
        else if (content != null)
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
        }

        if (content != null)
        {
            requestDataService.ContentId = content.Id;
            requestDataService.IncludeChildren = content.IncludeChildren;
            requestDataService.LanguageIsoCode = CultureInfo.CurrentCulture.Name;
        }

        await next(context);
    }

    private static Domain? MatchDomainWithContent(string url, List<Domain> domains)
    {
        // Extract host and path from the provided URL
        var uri = new Uri(url);
        var requestHost = uri.Host.ToLower();
        var requestPath = uri.AbsolutePath.TrimStart('/').ToLower(); // Trim leading slash for consistency

        // Find the matching domain
        var matchingDomain = domains.FirstOrDefault(domain =>
        {
            var domainUrl = domain.Url?.ToLower();

            // Handle domain with path
            if (domainUrl != null && domainUrl.Contains('/'))
            {
                var domainParts = domainUrl.Split('/', 2);
                var domainHost = domainParts[0];
                var domainPath = domainParts.Length > 1 ? domainParts[1] : string.Empty;

                return requestHost == domainHost && requestPath.StartsWith(domainPath);
            }

            // Handle domain without path
            return requestHost == domainUrl;
        });

        return matchingDomain;
    }
}

public static class CustomCultureMiddlewareExtensions
{
    public static IApplicationBuilder UseCustomCulture(
        this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<EntryContentMiddleware>();
    }
}