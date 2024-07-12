﻿using System.Security.Cryptography;
using System.Text;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Content.Commands;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Shared.Services;

namespace ZauberCMS.Core.Content.Handlers;

public class GetContentHandler(IServiceProvider serviceProvider, ICacheService cacheService) 
    : IRequestHandler<GetContentCommand, Models.Content?>
{
    public async Task<Models.Content?> Handle(GetContentCommand request, CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ZauberDbContext>();
        var cacheKey = GenerateCacheKey(request, dbContext);

        if (request.Cached)
        {
            return await cacheService.GetSetCachedItemAsync(cacheKey, async () => await FetchContentAsync(request, dbContext, cancellationToken));
        }

        return await FetchContentAsync(request, dbContext, cancellationToken);
    }

    private string GenerateCacheKey(GetContentCommand request, ZauberDbContext dbContext)
    {
        var query = BuildQuery(request, dbContext);
        var queryString = query.ToQueryString();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(queryString));
        return typeof(Models.Content).ToCacheKey(Convert.ToBase64String(hash));
    }

    private IQueryable<Models.Content> BuildQuery(GetContentCommand request, ZauberDbContext dbContext)
    {
        var query = dbContext.Contents
            .Include(x => x.ContentType)
            .Include(x => x.PropertyData)
            .AsSplitQuery()
            .AsQueryable();

        if (request.AsNoTracking)
        {
            query = query.AsNoTracking();
        }
        
        if (request.IncludeParent)
        {
            query = query.Include(x => x.Parent);
        }
        
        if (request.IncludeChildren)
        {
            query = query.Include(x => x.Children);
        }

        if (request.Id != null)
        {
            return query.Where(x => x.Id == request.Id);
        }
        
        if (!request.ContentTypeAlias.IsNullOrWhiteSpace())
        {
            return query.Where(x => x.ContentType != null && x.ContentType.Alias == request.ContentTypeAlias);
        }

        return query;
    }

    private async Task<Models.Content?> FetchContentAsync(GetContentCommand request, ZauberDbContext dbContext, CancellationToken cancellationToken)
    {
        var query = BuildQuery(request, dbContext);
        return await query.FirstOrDefaultAsync(cancellationToken: cancellationToken);
    }
}