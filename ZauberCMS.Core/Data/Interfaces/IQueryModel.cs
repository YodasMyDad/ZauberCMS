namespace ZauberCMS.Core.Data.Interfaces;

public interface IQueryModel
{
    string? Name { get; }
    Task<IEnumerable<object>> ExecuteQuery(ZauberDbContext dbContext, CancellationToken cancellationToken);
}