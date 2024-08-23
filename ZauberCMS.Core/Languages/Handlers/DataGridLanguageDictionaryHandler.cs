using System.Linq.Dynamic.Core;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Languages.Commands;
using ZauberCMS.Core.Languages.Models;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Languages.Handlers;

public class DataGridLanguageDictionaryHandler(IServiceProvider serviceProvider) : IRequestHandler<DataGridLanguageDictionaryCommand, DataGridResult<LanguageDictionary>>
{
    public async Task<DataGridResult<LanguageDictionary>> Handle(DataGridLanguageDictionaryCommand request, CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ZauberDbContext>();
        
        var result = new DataGridResult<LanguageDictionary>();

        // Now you have the DbSet<T> and can query it
        var query = dbContext.LanguageDictionaries
            .Include(x => x.Texts)
            .AsSplitQuery()
            .AsTracking()
            .AsQueryable();
        
        if (!string.IsNullOrEmpty(request.Filter))
        {
            // Filter via the Where method
            query = query.Where(request.Filter);
        }

        if (!string.IsNullOrEmpty(request.Order))
        {
            // Sort via the OrderBy method
            query = query.OrderBy(request.Order);
        }
        else
        {
            query = query.OrderBy(x => x.Key);
        }

        // Important!!! Make sure the Count property of RadzenDataGrid is set.
        result.Count = query.Count();

        // Perform paging via Skip and Take.
        result.Items = await query.Skip(request.Skip).Take(request.Take).ToListAsync(cancellationToken: cancellationToken);

        return result;
    }
}