using System.Globalization;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Content.Commands;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Data;

namespace ZauberCMS.Core.Content.Handlers;

public class ProcessEntryContentHandler(IServiceProvider serviceProvider, IMediator mediator)
    : IRequestHandler<ProcessEntryContentCommand, EntryContentResult?>
{
    public async Task<EntryContentResult?> Handle(ProcessEntryContentCommand request, CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ZauberDbContext>();
        
        var domains = await mediator.Send(new CachedDomainsCommand(), cancellationToken);
        var contentWithLanguages = await mediator.Send(new GetContentLanguagesCommand(), cancellationToken);

        var contentQueryable = dbContext.Contents
            .AsNoTracking()
            .Include(x => x.ContentType)
            .Include(x => x.Language);

        Domain? matchedDomain = null;

        if (request.IsRootContent && domains.Count != 0)
        {
            if (request.FullUrl != null) matchedDomain = MatchDomainWithContent(request.FullUrl, domains);
        }

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

        if (matchedDomain?.Language?.LanguageIsoCode != null)
        {
            SetCulture(matchedDomain.Language.LanguageIsoCode);
        }
        else if (content?.Language?.LanguageIsoCode != null)
        {
            SetCulture(content.Language.LanguageIsoCode);
        }
        else if (content != null)
        {
            foreach (var guid in content.Path)
            {
                if (contentWithLanguages.TryGetValue(guid, out var language))
                {
                    SetCulture(language);
                    break;
                }
            }
        }

        return content != null
            ? new EntryContentResult
            {
                ContentId = content.Id,
                IncludeChildren = content.IncludeChildren,
                LanguageIsoCode = CultureInfo.CurrentCulture.Name
            }
            : null;
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
                var domainParts = domainUrl.Split('/', 2);
                var domainHost = domainParts[0];
                var domainPath = domainParts.Length > 1 ? domainParts[1] : string.Empty;

                return requestHost == domainHost && requestPath.StartsWith(domainPath);
            }

            return requestHost == domainUrl;
        });
    }

    private static void SetCulture(string languageIsoCode)
    {
        var cultureInfo = new CultureInfo(languageIsoCode);
        CultureInfo.CurrentCulture = cultureInfo;
        CultureInfo.CurrentUICulture = cultureInfo;
    }
}
