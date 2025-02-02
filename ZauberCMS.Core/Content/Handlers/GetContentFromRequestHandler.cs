using System.Globalization;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ZauberCMS.Core.Content.Commands;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Languages.Commands;
using ZauberCMS.Core.Settings;

namespace ZauberCMS.Core.Content.Handlers;

public class GetContentFromRequestHandler(
    IServiceProvider serviceProvider,
    IMediator mediator,
    IOptions<ZauberSettings> settings)
    : IRequestHandler<GetContentFromRequestCommand, EntryModel>
{
    public async Task<EntryModel> Handle(GetContentFromRequestCommand request, CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ZauberDbContext>();

        var entryModel = new EntryModel();

        var contentQueryable = dbContext.Contents
            .AsNoTracking()
            .Include(x => x.ContentType)
            .Include(x => x.Language);

        // Get the list of domains and content languages
        var domains = await mediator.Send(new CachedDomainsCommand(), cancellationToken);
        var contentWithLanguages = await mediator.Send(new GetContentLanguagesCommand(), cancellationToken);

        // Match the domain
        var matchedDomain = MatchDomainWithContent(request.Url ?? string.Empty, domains);

        var content = request.IsRootContent
            ? matchedDomain != null
                ? await contentQueryable
                    .Select(c => new
                        { c.Id, c.InternalRedirectId, c.ContentType!.IncludeChildren, c.Language, c.Path })
                    .FirstOrDefaultAsync(x => x.Id == matchedDomain.ContentId, cancellationToken)
                : await contentQueryable
                    .Where(c => c.IsRootContent && c.Published)
                    .Select(c => new
                        { c.Id, c.InternalRedirectId, c.ContentType!.IncludeChildren, c.Language, c.Path })
                    .FirstOrDefaultAsync(cancellationToken)
            : await contentQueryable
                .Where(c => c.Url == request.Slug && c.Published)
                .Select(c => new { c.Id, c.InternalRedirectId, c.ContentType!.IncludeChildren, c.Language, c.Path })
                .FirstOrDefaultAsync(cancellationToken);

        if (content?.InternalRedirectId != null && content.InternalRedirectId != Guid.Empty)
        {
            var internalRedirectIdValue = content.InternalRedirectId.Value;
            content = await contentQueryable
                .Where(c => c.Id == internalRedirectIdValue)
                .Select(c => new { c.Id, c.InternalRedirectId, c.ContentType!.IncludeChildren, c.Language, c.Path })
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (content == null)
        {
            return entryModel;
        }

        string? languageIsoCode = null;

        // Use the matched domain's language if available
        if (matchedDomain?.Language?.LanguageIsoCode != null)
        {
            languageIsoCode = matchedDomain.Language.LanguageIsoCode;
        }
        else if (contentWithLanguages.TryGetValue(request.Slug ?? string.Empty,
                     out var contentLanguage)) // Use slug to find language
        {
            languageIsoCode = contentLanguage;
        }

        // Set the culture based on the resolved language
        if (languageIsoCode.IsNullOrWhiteSpace())
        {
            foreach (var guid in content.Path)
            {
                if (contentWithLanguages.TryGetValue(guid, out var language))
                {
                    languageIsoCode = language;
                    break;
                }
            }

            if (languageIsoCode.IsNullOrWhiteSpace())
            {
                // Set to default   
                languageIsoCode = settings.Value.AdminDefaultLanguage;
            }
        }

        entryModel.LanguageIsoCode = languageIsoCode;
        
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

        entryModel.Content = fullContent;

        var allLanguageData = await mediator.Send(new GetCachedAllLanguageDictionariesCommand(), cancellationToken);
        
        if (allLanguageData.TryGetValue(languageIsoCode, out var lng))
        {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            if (lng != null)
            {
                entryModel.LanguageKeys = lng;
            }
        }
        
        return entryModel;
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