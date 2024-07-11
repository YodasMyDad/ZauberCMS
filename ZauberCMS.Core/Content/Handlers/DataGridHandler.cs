using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Content.Commands;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Shared.Models;
using System.Linq.Dynamic.Core;

namespace ZauberCMS.Core.Content.Handlers;

public class DataGridHandler<T>(IServiceProvider serviceProvider) : IRequestHandler<DataGridCommand<T>, DataGridResult<T>> where T : class
{
    public async Task<DataGridResult<T>> Handle(DataGridCommand<T> request, CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ZauberDbContext>();
        
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
        
        if (!string.IsNullOrEmpty(request.Filter))
        {
            // Filter via the Where method
            query = query.Where(request.Filter);
        }

        if (!string.IsNullOrEmpty(request.OrderBy))
        {
            // Sort via the OrderBy method
            query = query.OrderBy(request.OrderBy);
        }

        // Important!!! Make sure the Count property of RadzenDataGrid is set.
        result.Count = query.Count();

        // Perform paging via Skip and Take.
        result.Items = await query.Skip(request.Skip).Take(request.Take).ToListAsync(cancellationToken: cancellationToken);

        return result;
    }
}