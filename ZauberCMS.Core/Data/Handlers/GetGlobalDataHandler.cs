using System.Security.Cryptography;
using System.Text;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Data.Commands;
using ZauberCMS.Core.Data.Models;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Shared.Services;

namespace ZauberCMS.Core.Data.Handlers;

public class GetGlobalDataHandler(IServiceProvider serviceProvider, ICacheService cacheService) : IRequestHandler<GetGlobalDataCommand, GlobalData?>
{
    public async Task<GlobalData?> Handle(GetGlobalDataCommand request, CancellationToken cancellationToken)
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
    
    private static string GenerateCacheKey(GetGlobalDataCommand request, ZauberDbContext dbContext)
    {
        var query = BuildQuery(request, dbContext);
        var queryString = query.ToQueryString();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(queryString));
        return typeof(GlobalData).ToCacheKey(Convert.ToBase64String(hash));
    }

    private static IQueryable<GlobalData> BuildQuery(GetGlobalDataCommand request, ZauberDbContext dbContext)
    {
        return dbContext.GlobalDatas.AsNoTracking()
            .Where(x => x.Alias == request.Alias);
    }
    
    private static async Task<GlobalData?> FetchContentAsync(GetGlobalDataCommand request, ZauberDbContext dbContext, CancellationToken cancellationToken)
    {
        var query = BuildQuery(request, dbContext);
        return await query.FirstOrDefaultAsync(cancellationToken: cancellationToken);
    }
}