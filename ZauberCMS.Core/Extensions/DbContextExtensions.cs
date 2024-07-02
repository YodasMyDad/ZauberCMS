using Serilog;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Extensions;

public static class DbContextExtensions
{
    public static IQueryable<T>? ToTyped<T>(this ZauberDbContext context) where T : class
    {
        try
        {
            var dbSet = context.Set<T>();
            return dbSet.AsQueryable();
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Unable to get {nameof(T)} as DbSet");
        }

        return null;
    }
    
    public static async Task<HandlerResult<T>> SaveChangesAndLog<T>(this ZauberDbContext context, T? entity,
        HandlerResult<T> crudResult, CancellationToken cancellationToken)
    {
        try
        {
            var isSaved = await context.SaveChangesAsync(cancellationToken);
            crudResult.Success = true;
            if (entity != null)
            {
                crudResult.Entity = entity;
            }
            if (isSaved <= 0)
            {
                Log.Warning($"{nameof(T)} returned 0 items saved when creating or updating");
            }
        }
        catch (Exception ex)
        {
            crudResult.Success = false;
            crudResult.AddMessage($"{ex.Message} - {ex.InnerException?.Message}", ResultMessageType.Error);
            Log.Error(ex, $"{nameof(T)} not saved using SaveChangesAsync");
        }

        return crudResult;
    }

    /// <summary>
    /// Returns paginated list from a queryable
    /// </summary>
    /// <param name="items"></param>
    /// <param name="pageIndex"></param>
    /// <param name="pageSize"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static PaginatedList<T> ToPaginatedList<T>(this IQueryable<T> items, int pageIndex, int pageSize)
    {
        return new PaginatedList<T>(items, pageIndex, pageSize);
    }
}