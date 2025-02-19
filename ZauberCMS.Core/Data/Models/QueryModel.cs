using Microsoft.EntityFrameworkCore;
using ZauberCMS.Core.Data.Interfaces;

namespace ZauberCMS.Core.Data.Models;

public class QueryModel<T> : IQueryModel
{
    public string? Name { get; set; }
    public Func<ZauberDbContext, IQueryable<T>> Query { get; set; } = null!;

    public async Task<IEnumerable<object>> ExecuteQuery(ZauberDbContext dbContext, CancellationToken cancellationToken)
    {
        var queryResult = await Query(dbContext).ToListAsync(cancellationToken);
        return queryResult.Cast<object>();
    }
}