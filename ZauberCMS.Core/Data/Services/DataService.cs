using System.Linq.Dynamic.Core;
using System.Security.Cryptography;
using System.Text;
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
    IServiceScopeFactory serviceScopeFactory,
    ICacheService cacheService,
    ExtensionManager extensionManager)
    : IDataService
{
    /// <summary>
    /// Retrieves global data by alias, optionally from cache.
    /// </summary>
    /// <param name="parameters">Alias and caching flag.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Global data or null.</returns>
    public async Task<GlobalData?> GetGlobalDataAsync(GetGlobalDataParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var query = BuildQuery(parameters, dbContext);
        if (parameters.Cached)
        {
            var cacheKey = query.GenerateCacheKey<GlobalData>();
            return await cacheService.GetSetCachedItemAsync(cacheKey, async () => await query.FirstOrDefaultAsync(cancellationToken: cancellationToken));
        }

        return await query.FirstOrDefaultAsync(cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Creates or updates a global data value by alias.
    /// </summary>
    /// <param name="parameters">Alias and value.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result including success and messages.</returns>
    public async Task<HandlerResult<GlobalData>> SaveGlobalDataAsync(SaveGlobalDataParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        
        var handlerResult = new HandlerResult<GlobalData>();

        if (!parameters.Alias.IsNullOrWhiteSpace() && !parameters.Data.IsNullOrWhiteSpace())
        {
            // Get the DB version
            var gData = dbContext.GlobalDatas
                .FirstOrDefault(x => x.Alias == parameters.Alias);

            if (gData == null)
            {
                gData = new GlobalData{Alias = parameters.Alias, Data = parameters.Data};
                dbContext.GlobalDatas.Add(gData);
            }
            else
            {
                gData.Data = parameters.Data;   
                gData.DateUpdated = DateTime.UtcNow;
            }
            
            return await dbContext.SaveChangesAndLog(gData, handlerResult, cacheService, extensionManager, cancellationToken);
        }

        handlerResult.AddMessage("GlobalData is null", ResultMessageType.Error);
        return handlerResult;
    }

    /// <summary>
    /// Executes a batch of queries and returns a named result set for each.
    /// </summary>
    /// <param name="parameters">Collection of named queries to execute.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dictionary of name to query results.</returns>
    public async Task<Dictionary<string, IEnumerable<object>>> MultiQueryAsync(MultiQueryParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();

        var results = new Dictionary<string, IEnumerable<object>>();

        foreach (var query in parameters.Queries)
        {
            var queryResult = await query.ExecuteQuery(dbContext, cancellationToken);
            if (query.Name != null) results.Add(query.Name, queryResult);
        }

        return results;
    }

    /// <summary>
    /// Generic data grid query for any DbSet<T> with dynamic filtering, ordering and paging.
    /// </summary>
    /// <param name="parameters">Grid options including filter, orderBy, skip and take.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <typeparam name="T">Entity type which implements ITreeItem.</typeparam>
    /// <returns>Data grid result including total count and items.</returns>
    /// <exception cref="ArgumentException">The filter or sort string is not valid, or uses a secret column
    /// (password hash, security stamp) or anything other than the entity's properties and simple value methods.</exception>
    public async Task<DataGridResult<T>> GetDataGridAsync<T>(DataGridParameters<T> parameters, CancellationToken cancellationToken = default) where T : class, ITreeItem
    {
        using var scope = serviceScopeFactory.CreateScope();
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

        // Now you have the DbSet<T> and can query it
        var query = dbSet.AsQueryable();

        // No global query filters exist, so the three soft-deleted types are filtered here (AGENTS.md rule 4).
        if (!parameters.IncludeDeleted)
        {
            query = query switch
            {
                IQueryable<Content.Models.Content> contents => (IQueryable<T>)contents.Where(x => !x.Deleted),
                IQueryable<Media.Models.Media> media => (IQueryable<T>)media.Where(x => !x.Deleted),
                IQueryable<Membership.Models.User> users => (IQueryable<T>)users.Where(x => !x.Deleted),
                _ => query
            };
        }

        // The strings are parsed into expressions; GridQueryGuard refuses anything but ordinary column use.
        query = GridQueryGuard.Filter(query, parameters.Filter);
        query = GridQueryGuard.OrderBy(query, parameters.OrderBy);

        // Paging needs one order every time: rows with the same sort value (or no sort at all) could move from one
        // page to the next between requests (ZA-115)
        query = string.IsNullOrWhiteSpace(parameters.OrderBy)
            ? query.OrderBy(x => x.Id)
            : ((IOrderedQueryable<T>)query).ThenBy(x => x.Id);

        // Important!!! Make sure the Count property of RadzenDataGrid is set.
        result.Count = await query.CountAsync(cancellationToken);

        // Perform paging via Skip and Take.
        result.Items = await query.Skip(parameters.Skip).Take(parameters.Take).ToListAsync(cancellationToken: cancellationToken);

        return result;
    }

    private static IQueryable<GlobalData> BuildQuery(GetGlobalDataParameters parameters, IZauberDbContext dbContext)
    {
        return dbContext.GlobalDatas.AsNoTracking()
            .Where(x => x.Alias == parameters.Alias);
    }
}
