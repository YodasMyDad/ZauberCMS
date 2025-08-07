using System.Security.Cryptography;
using System.Text;
using System.Linq.Dynamic.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Data.Interfaces;
using ZauberCMS.Core.Data.Models;
using ZauberCMS.Core.Data.Parameters;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Plugins;
using ZauberCMS.Core.Shared.Interfaces;
using ZauberCMS.Core.Shared.Models;
using ZauberCMS.Core.Shared.Services;

namespace ZauberCMS.Core.Data.Services;

public class DataService(
    IServiceProvider serviceProvider,
    ICacheService cacheService,
    ExtensionManager extensionManager) : IDataService
{
    public async Task<GlobalData?> GetGlobalDataAsync(GetGlobalDataParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();

        var cacheKey = GenerateCacheKey(parameters, dbContext);

        if (parameters.Cached)
        {
            return await cacheService.GetSetCachedItemAsync(cacheKey, async () => await FetchGlobalDataAsync(parameters, dbContext, cancellationToken));
        }

        return await FetchGlobalDataAsync(parameters, dbContext, cancellationToken);
    }

    public async Task<HandlerResult<GlobalData>> SaveGlobalDataAsync(SaveGlobalDataParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();

        var handlerResult = new HandlerResult<GlobalData>();

        if (!parameters.Alias.IsNullOrWhiteSpace() && !parameters.Data.IsNullOrWhiteSpace())
        {
            var globalData = dbContext.GlobalDatas
                .FirstOrDefault(x => x.Alias == parameters.Alias);

            if (globalData == null)
            {
                globalData = new GlobalData { Alias = parameters.Alias, Data = parameters.Data };
                dbContext.GlobalDatas.Add(globalData);
            }
            else
            {
                globalData.Data = parameters.Data;
                globalData.DateUpdated = DateTime.UtcNow;
            }

            return await dbContext.SaveChangesAndLog(globalData, handlerResult, cacheService, extensionManager, cancellationToken);
        }

        handlerResult.AddMessage("GlobalData is null", ResultMessageType.Error);
        return handlerResult;
    }

    public async Task<Dictionary<string, IEnumerable<object>>> MultiQueryAsync(MultiQueryParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();

        var results = new Dictionary<string, IEnumerable<object>>();

        foreach (var query in parameters.Queries)
        {
            var queryResult = await query.ExecuteQuery(dbContext, cancellationToken);
            if (query.Name != null)
            {
                results.Add(query.Name, queryResult);
            }
        }

        return results;
    }

    public async Task<DataGridResult<T>> GetDataGridAsync<T>(DataGridParameters<T> parameters, CancellationToken cancellationToken = default) where T : class, ITreeItem
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();

        // Use reflection to get the DbSet<T>
        var dbSetProperty = dbContext.GetType().GetProperties()
            .FirstOrDefault(p => p.PropertyType == typeof(DbSet<T>));

        if (dbSetProperty == null)
        {
            throw new InvalidOperationException($"DbSet<{typeof(T).Name}> is not found in the DbContext.");
        }

        if (dbSetProperty.GetValue(dbContext) is not DbSet<T> dbSet)
        {
            throw new InvalidOperationException($"Unable to get the DbSet<{typeof(T).Name}> from the DbContext.");
        }

        var result = new DataGridResult<T>();

        var query = dbSet.AsQueryable();

        if (!string.IsNullOrEmpty(parameters.Filter))
        {
            query = query.Where(parameters.Filter);
        }

        if (!string.IsNullOrEmpty(parameters.OrderBy))
        {
            query = query.OrderBy(parameters.OrderBy);
        }

        result.Count = query.Count();
        result.Items = await query.Skip(parameters.Skip).Take(parameters.Take).ToListAsync(cancellationToken: cancellationToken);

        return result;
    }

    private static string GenerateCacheKey(GetGlobalDataParameters parameters, IZauberDbContext dbContext)
    {
        var query = BuildGlobalDataQuery(parameters, dbContext);
        var queryString = query.ToQueryString();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(queryString));
        return typeof(GlobalData).ToCacheKey(Convert.ToBase64String(hash));
    }

    private static IQueryable<GlobalData> BuildGlobalDataQuery(GetGlobalDataParameters parameters, IZauberDbContext dbContext)
    {
        return dbContext.GlobalDatas.AsNoTracking()
            .Where(x => x.Alias == parameters.Alias);
    }

    private static async Task<GlobalData?> FetchGlobalDataAsync(GetGlobalDataParameters parameters, IZauberDbContext dbContext, CancellationToken cancellationToken)
    {
        var query = BuildGlobalDataQuery(parameters, dbContext);
        return await query.FirstOrDefaultAsync(cancellationToken: cancellationToken);
    }
}