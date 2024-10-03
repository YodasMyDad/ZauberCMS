using System.Linq.Dynamic.Core;
using System.Security.Cryptography;
using System.Text;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using ZauberCMS.Core.Content.Commands;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Shared.Models;
using ZauberCMS.Core.Shared.Services;

namespace ZauberCMS.Core.Content.Handlers;

public class QueryContentHandler(IServiceProvider serviceProvider, ICacheService cacheService)
    : IRequestHandler<QueryContentCommand, PaginatedList<Models.Content>>
{
    public async Task<PaginatedList<Models.Content>> Handle(QueryContentCommand request, CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ZauberDbContext>();
        var cacheKey = GenerateCacheKey(request, dbContext);
        
        if (request.Cached)
        {
            return (await cacheService.GetSetCachedItemAsync(cacheKey, async () => await FetchContentAsync(request, dbContext, cancellationToken)))!;
        }

        return await FetchContentAsync(request, dbContext, cancellationToken);
    }

    private string GenerateCacheKey(QueryContentCommand request, ZauberDbContext dbContext)
    {
        var query = BuildQuery(request, dbContext);
        var queryString = query.ToQueryString();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(queryString));
        return typeof(Models.Content).ToCacheKey(Convert.ToBase64String(hash));
    }

    private IQueryable<Models.Content> BuildQuery(QueryContentCommand request, ZauberDbContext dbContext)
    {
        var query = dbContext.Contents.Include(x => x.ContentType)
            .Include(x => x.PropertyData).AsSplitQuery().AsQueryable();

        if (request.Query != null)
        {
            query = request.Query;
        }
        else
        {
            if (request.OnlyUnpublished)
            {
                query = query.Include(x => x.UnpublishedContent);
                query = query.Where(x => x.UnpublishedContentId != null || x.Published == false);
            }
            else
            {
                query = !request.IncludeUnpublished ? query.Where(x => x.Published) : query.Include(x => x.UnpublishedContent);       
            }

            if (request.IsDeleted != null)
            {
                query = query.Where(x => x.Deleted == request.IsDeleted);
            }
            
            if (request.IncludeChildren)
            {
                query = request.IncludeUnpublished ? query.Include(x => x.Children).ThenInclude(x => x.UnpublishedContent) 
                    : query.Include(x => x.Children.Where(c => c.Published));
            }

            if (request.TagSlugs.Any())
            {
                query = (from content in query
                    join tagItem in dbContext.TagItems on content.Id equals tagItem.ItemId
                    join tag in dbContext.Tags on tagItem.TagId equals tag.Id
                    where request.TagSlugs.Contains(tag.Slug)
                    select content).Distinct();
                
                /*query = query.Where(content => dbContext.TagItems
                    .Any(tagItem => tagItem.ItemId == content.Id &&
                                    dbContext.Tags.Any(tag => tag.Id == tagItem.TagId && tagSlugs.Contains(tag.Slug))));*/
            }
            
            if (request.AsNoTracking)
            {
                query = query.AsNoTracking();
            }

            if (!string.IsNullOrWhiteSpace(request.SearchTerm))
            {
                query = query.Where(x => x.Name != null && x.Name.ToLower().Contains(request.SearchTerm.ToLower()));
            }

            if (!string.IsNullOrWhiteSpace(request.ContentTypeAlias))
            {
                var contentType = dbContext.ContentTypes.AsNoTracking().FirstOrDefault(x => x.Alias == request.ContentTypeAlias);
                // Bit hacky, but it will return content if we don't do this
                request.ContentTypeId = contentType?.Id ?? Guid.Empty;
            }

            if (request.ContentTypeId != null)
            {
                query = query.Where(x => x.ContentTypeId == request.ContentTypeId);
            }

            if (request.ParentId != null)
            {
                query = query.Where(x => x.ParentId == request.ParentId);
            }

            var idCount = request.Ids.Count;
            if (idCount != 0)
            {
                query = query.Where(x => request.Ids.Contains(x.Id));
                request.AmountPerPage = idCount;
            }
        }

        if (request.WhereClause != null)
        {
            query = query.Where(request.WhereClause);
        }

        query = request.OrderBy switch
        {
            GetContentsOrderBy.DateUpdated => query.OrderBy(p => p.DateUpdated),
            GetContentsOrderBy.DateUpdatedDescending => query.OrderByDescending(p => p.DateUpdated),
            GetContentsOrderBy.DateCreated => query.OrderBy(p => p.DateCreated),
            GetContentsOrderBy.DateCreatedDescending => query.OrderByDescending(p => p.DateCreated),
            GetContentsOrderBy.SortOrder => query.OrderBy(p => p.SortOrder),
            _ => query.OrderByDescending(p => p.DateUpdated)
        };

        return query;
    }

    private Task<PaginatedList<Models.Content>> FetchContentAsync(QueryContentCommand request, ZauberDbContext dbContext, CancellationToken cancellationToken)
    {
        var query = BuildQuery(request, dbContext);
        return Task.FromResult(query.ToPaginatedList(request.PageIndex, request.AmountPerPage));
    }
}