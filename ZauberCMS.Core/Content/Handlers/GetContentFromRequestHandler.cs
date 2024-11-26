using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Content.Commands;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Languages.Commands;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Content.Handlers;

public class GetContentFromRequestHandler(IServiceProvider serviceProvider, IMediator mediator, RequestCulture requestCulture)
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

        Domain? matchedDomain = null;

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
        
        // TODO - Get from middleware
        if (requestCulture.LanguageIsoCode != null)
        {
            allLanguageData.TryGetValue(requestCulture.LanguageIsoCode, out var language);
            if (language != null)
            {
                entryModel.LanguageKeys = language;
            }
        }

        return entryModel;
    }
}