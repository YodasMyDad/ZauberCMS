using System.Globalization;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Content.Commands;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Data;

namespace ZauberCMS.Core.Content.Handlers;

public class GetContentBySlugHandler(IServiceProvider serviceProvider, IMediator mediator)
    : IRequestHandler<GetContentBySlugCommand, Models.Content?>
{
    public async Task<Models.Content?> Handle(GetContentBySlugCommand request, CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ZauberDbContext>();

        var domains = await mediator.Send(new CachedDomainsCommand(), cancellationToken);
        Domain? matchedDomain = null;
        if (request.IsRootContent && domains.Count != 0)
        {
            matchedDomain = MatchDomainWithContent(request.Url, domains);
        }

        // If this is root content, get the first one with minimal data
        var content = request.IsRootContent
            ? matchedDomain != null
                ? await dbContext.Contents
                    .AsNoTracking()
                    .Include(x => x.ContentType)
                    .Select(c => new { c.Id, c.InternalRedirectId, c.ContentType!.IncludeChildren })
                    .FirstOrDefaultAsync(x => x.Id == matchedDomain.ContentId, cancellationToken: cancellationToken)
                : await dbContext.Contents
                    .AsNoTracking()
                    .Include(x => x.ContentType)
                    .Where(c => c.IsRootContent && c.Published)
                    .Select(c => new { c.Id, c.InternalRedirectId, c.ContentType!.IncludeChildren })
                    .FirstOrDefaultAsync(cancellationToken: cancellationToken)
            : await dbContext.Contents
                .AsNoTracking()
                .Include(x => x.ContentType)
                .Where(c => c.Url == request.Slug && c.Published)
                .Select(c => new { c.Id, c.InternalRedirectId, c.ContentType!.IncludeChildren })
                .FirstOrDefaultAsync(cancellationToken: cancellationToken);

        // If this content has an internal redirect id, get that content's ID instead
        if (content?.InternalRedirectId != null && content.InternalRedirectId != Guid.Empty &&
            !request.IgnoreInternalRedirect)
        {
            var internalRedirectIdValue = content.InternalRedirectId.Value;
            content = await dbContext.Contents
                .AsNoTracking()
                .Include(x => x.ContentType)
                .Where(c => c.Id == internalRedirectIdValue)
                .Select(c => new { c.Id, c.InternalRedirectId, c.ContentType!.IncludeChildren })
                .FirstOrDefaultAsync(cancellationToken: cancellationToken);
        }

        if (content == null)
        {
            return null;
        }

        // set the language

        // Now we perform the more expensive query to fetch the content with includes
        var query = dbContext.Contents
            .AsNoTracking()
            .Include(x => x.PropertyData)
            .Include(x => x.Parent)
            .Include(x => x.ContentType)
            .Include(x => x.Language)
            .AsSplitQuery()
            .AsQueryable();

        if (request.IncludeChildren || content.IncludeChildren)
        {
            query = query.Include(x => x.Children);
        }

        var fullContent = await query
            .FirstOrDefaultAsync(c => c.Id == content.Id, cancellationToken: cancellationToken);

        // Domain overrides language set on content
        if (matchedDomain?.Language?.LanguageIsoCode != null)
        {
            var cultureInfo = new CultureInfo(matchedDomain.Language.LanguageIsoCode);
            CultureInfo.DefaultThreadCurrentCulture = cultureInfo;
            CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;
        }
        else
        // Set the language
        if (fullContent?.Language?.LanguageIsoCode != null)
        {
            var cultureInfo = new CultureInfo(fullContent.Language.LanguageIsoCode);
            CultureInfo.DefaultThreadCurrentCulture = cultureInfo;
            CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;
        }
        
        return fullContent;
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