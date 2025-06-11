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

public class GetRestrictedMediaUrlsHandler(IServiceProvider serviceProvider, ICacheService cacheService) 
    : IRequestHandler<GetRestrictedMediaUrls, Dictionary<string, Guid>>
{
    public async Task<Dictionary<string, Guid>> Handle(GetRestrictedMediaUrls request, CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var cacheKey = GenerateCacheKey(dbContext);

        if (request.Cached)
        {
            return await cacheService.GetSetCachedItemAsync(cacheKey, async () => await FetchRestrictedMediaUrlsAsync(dbContext, cancellationToken)) ?? new Dictionary<string, Guid>();
        }

        return await FetchRestrictedMediaUrlsAsync(dbContext, cancellationToken);
    }

    private static string GenerateCacheKey(IZauberDbContext dbContext)
    {
        var query = BuildQuery(dbContext);
        var queryString = query.ToQueryString();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(queryString));
        return typeof(Models.Media).ToCacheKey(Convert.ToBase64String(hash));
    }

    private static IQueryable<Models.Media> BuildQuery(IZauberDbContext dbContext)
    {
        return dbContext.Medias.AsNoTracking().Where(x => x.RequiresAuthentication);
    }

    private static async Task<Dictionary<string, Guid>> FetchRestrictedMediaUrlsAsync(IZauberDbContext dbContext, CancellationToken cancellationToken)
    {
        var query = BuildQuery(dbContext);
        return await query
            .Select(x => new { x.Url, x.Id })
            .ToDictionaryAsync(x => x.Url ?? string.Empty, x => x.Id, cancellationToken: cancellationToken);
    }
}
