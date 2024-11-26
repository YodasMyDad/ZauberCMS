using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Content.Commands;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Languages.Commands;
using ZauberCMS.Core.Shared.Services;

namespace ZauberCMS.Core.Content.Handlers;

public class GetContentFromRequestHandler(IServiceProvider serviceProvider, IMediator mediator)
    : IRequestHandler<GetContentFromRequestCommand, EntryModel>
{
    public async Task<EntryModel> Handle(GetContentFromRequestCommand request, CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ZauberDbContext>();
        
        var entryModel = new EntryModel();
        
        var entryContentResult = await mediator.Send(new ProcessEntryContentCommand
        {
            FullUrl = request.Url,
            Slug = request.Slug,
            IsRootContent = request.IsRootContent
        }, cancellationToken); 
        
        if (entryContentResult == null)
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

        if (request.IncludeChildren || entryContentResult.IncludeChildren)
        {
            query = query.Include(x => x.Children);
        }

        var fullContent = await query
            .FirstOrDefaultAsync(c => c.Id == entryContentResult.ContentId, cancellationToken: cancellationToken);
        
        entryModel.Content = fullContent;

        var allLanguageData = await mediator.Send(new GetCachedAllLanguageDictionariesCommand(), cancellationToken);
        if (entryContentResult.LanguageIsoCode != null)
        {
            allLanguageData.TryGetValue(entryContentResult.LanguageIsoCode, out var language);
            if (language != null)
            {
                entryModel.LanguageKeys = language;
            }
        }

        return entryModel;
    }
}