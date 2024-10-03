using System.Security.Cryptography;
using System.Text;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Media.Commands;
using ZauberCMS.Core.Shared.Services;

namespace ZauberCMS.Core.Media.Handlers;

public class GetMediaHandler(IServiceProvider serviceProvider, ICacheService cacheService)
    : IRequestHandler<GetMediaCommand, Models.Media?>
{
    public async Task<Models.Media?> Handle(GetMediaCommand request, CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ZauberDbContext>();
        var cacheKey = GenerateCacheKey(request, dbContext);

        if (request.Cached)
        {
            return await cacheService.GetSetCachedItemAsync(cacheKey, async () => await FetchMediaAsync(request, dbContext, cancellationToken));
        }

        return await FetchMediaAsync(request, dbContext, cancellationToken);
    }

    private static string GenerateCacheKey(GetMediaCommand request, ZauberDbContext dbContext)
    {
        var query = BuildQuery(request, dbContext);
        var queryString = query.ToQueryString();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(queryString));
        return typeof(Models.Media).ToCacheKey(Convert.ToBase64String(hash));
    }

    private static IQueryable<Models.Media> BuildQuery(GetMediaCommand request, ZauberDbContext dbContext)
    {
        var query = dbContext.Medias.AsQueryable();

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

            if (request.IncludeParent)
            {
                query = query.AsSplitQuery();
            }
        }

        if (request.MediaType != null)
        {
            query = query.Where(x => x.MediaType == request.MediaType);
        }

        if (request.Id != null)
        {
            query = query.Where(x => x.Id == request.Id);
        }

        return query;
    }

    private static async Task<Models.Media?> FetchMediaAsync(GetMediaCommand request, ZauberDbContext dbContext, CancellationToken cancellationToken)
    {
        var query = BuildQuery(request, dbContext);
        return await query.FirstOrDefaultAsync(cancellationToken: cancellationToken);
    }
}
