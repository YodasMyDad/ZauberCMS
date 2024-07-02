using System.Linq.Expressions;
using MediatR;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Audit.Commands;

public class QueryAuditsCommand : IRequest<PaginatedList<Models.Audit>>
{
    public bool AsNoTracking { get; set; } = true;
    public int PageIndex { get; set; } = 1;
    public int AmountPerPage { get; set; } = 10;
    
    public string? Username { get; set; }
    public GetAuditsOrderBy OrderBy { get; set; } = GetAuditsOrderBy.DateCreatedDescending;
    public Expression<Func<Models.Audit, bool>>? WhereClause { get; set; }
    public IQueryable<Models.Audit>? Query { get; set; }
}

public enum GetAuditsOrderBy
{
    DateCreated,
    DateCreatedDescending,
    Username
}