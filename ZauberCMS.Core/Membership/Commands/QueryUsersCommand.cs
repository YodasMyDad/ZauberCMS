using System.Linq.Expressions;
using MediatR;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Membership.Commands;

public class QueryUsersCommand : IRequest<PaginatedList<User>>
{
    /// <summary>
    /// Whether this query is to be cached
    /// </summary>
    public bool Cached { get; set; } 
    public List<string> Roles { get; set; } = [];
    public bool AsNoTracking { get; set; } = true;
    public List<Guid> Ids { get; set; } = [];
    public int PageIndex { get; set; } = 1;
    public int AmountPerPage { get; set; } = 10;
    public string? SearchTerm { get; set; }
    public GetUsersOrderBy OrderBy { get; set; } = GetUsersOrderBy.DateUpdatedDescending;
    public Expression<Func<User, bool>>? WhereClause { get; set; }
    public IQueryable<User>? Query { get; set; }
}

public enum GetUsersOrderBy
{
    DateUpdated,
    DateUpdatedDescending,
    DateCreated,
    DateCreatedDescending
}