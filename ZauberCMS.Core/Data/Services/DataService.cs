using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Data.Interfaces;
using ZauberCMS.Core.Data.Parameters;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Plugins;
using ZauberCMS.Core.Shared.Models;
using ZauberCMS.Core.Shared.Services;

namespace ZauberCMS.Core.Data.Services;

public class DataService(
    IServiceProvider serviceProvider,
    ICacheService cacheService,
    ExtensionManager extensionManager) : IDataService
{
    public async Task<object?> GetGlobalDataAsync(GetGlobalDataParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();

        var globalData = await dbContext.GlobalData
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Key == parameters.Key && x.DomainId == parameters.DomainId, cancellationToken);

        return globalData?.Value;
    }

    public async Task<HandlerResult<object>> SaveGlobalDataAsync(SaveGlobalDataParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var handlerResult = new HandlerResult<object>();

        var existingData = await dbContext.GlobalData
            .FirstOrDefaultAsync(x => x.Key == parameters.Key && x.DomainId == parameters.DomainId, cancellationToken);

        if (existingData != null)
        {
            existingData.Value = parameters.Value;
            existingData.DateUpdated = DateTime.UtcNow;
        }
        else
        {
            var newData = new Models.GlobalData
            {
                Key = parameters.Key,
                Value = parameters.Value,
                DomainId = parameters.DomainId
            };
            dbContext.GlobalData.Add(newData);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        handlerResult.Success = true;
        handlerResult.Entity = parameters.Value;

        return handlerResult;
    }

    public async Task<HandlerResult<object>> MultiQueryAsync(MultiQueryParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var handlerResult = new HandlerResult<object>();
        var results = new List<object>();

        foreach (var query in parameters.Queries)
        {
            // This would need to be implemented based on the specific query requirements
            // For now, just return empty results
            results.Add(new { Query = query, Results = new List<object>() });
        }

        handlerResult.Success = true;
        handlerResult.Items = results;

        return handlerResult;
    }

    public async Task<HandlerResult<T>> GetDataGridAsync<T>(DataGridParameters<T> parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var handlerResult = new HandlerResult<T>();

        // Get the DbSet for the type T
        var dbSet = dbContext.Set<T>();
        var query = dbSet.AsQueryable();

        if (parameters.AsNoTracking)
        {
            query = query.AsNoTracking();
        }

        if (parameters.WhereClause != null)
        {
            query = query.Where(parameters.WhereClause);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        if (!parameters.OrderBy.IsNullOrWhiteSpace())
        {
            query = query.OrderBy(parameters.OrderBy);
        }

        if (parameters.AmountPerPage > 0)
        {
            query = query.Skip(parameters.PageIndex * parameters.AmountPerPage).Take(parameters.AmountPerPage);
        }

        var items = await query.ToListAsync(cancellationToken);

        handlerResult.Success = true;
        handlerResult.Items = items;
        handlerResult.TotalCount = totalCount;

        return handlerResult;
    }
}