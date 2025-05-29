namespace ZauberCMS.Core.Data.Interfaces;

public interface IQueryModel
{
    string? Name { get; }
    Task<IEnumerable<object>> ExecuteQuery(IZauberDbContext dbContext, CancellationToken cancellationToken);
}