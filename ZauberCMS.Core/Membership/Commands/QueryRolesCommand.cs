using System.Linq.Expressions;
using MediatR;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Membership.Commands;

public class QueryRolesCommand : IRequest<PaginatedList<Role>>
{
    public List<string> Roles { get; set; } = [];
    public bool AsNoTracking { get; set; } = true;
    public List<Guid> Ids { get; set; } = [];
    public int PageIndex { get; set; } = 1;
    public int AmountPerPage { get; set; } = 10;
    public GetRolesOrderBy OrderBy { get; set; } = GetRolesOrderBy.DateUpdatedDescending;
    public Expression<Func<Role, bool>>? WhereClause { get; set; }
    public IQueryable<Role>? Query { get; set; }
}

public enum GetRolesOrderBy
{
    DateUpdated,
    DateUpdatedDescending,
    DateCreated,
    DateCreatedDescending,
    Name
}