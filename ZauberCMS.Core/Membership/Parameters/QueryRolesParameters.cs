using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Membership.Parameters;

public class QueryRolesParameters
{
    public List<string> Roles { get; set; } = [];
    public bool AsNoTracking { get; set; } = true;
    public List<Guid> Ids { get; set; } = [];
    public int PageIndex { get; set; } = 1;
    public int AmountPerPage { get; set; } = 10;
    public GetRolesOrderBy OrderBy { get; set; } = GetRolesOrderBy.DateUpdatedDescending;
    public Expression<Func<Role, bool>>? WhereClause { get; set; }
    public Func<IQueryable<Role>>? Query { get; set; }
    public string? SearchTerm { get; set; }

    /// <summary>Also load the role's members (<c>Role.UserRoles</c> with each <c>User</c>, recycled users
    /// included). Off by default: a role on a site with many members loaded all of them (ZA-056).</summary>
    public bool IncludeUsers { get; set; }
}

public enum GetRolesOrderBy
{
    DateUpdated,
    DateUpdatedDescending,
    DateCreated,
    DateCreatedDescending,
    Name,
    NameDescending
}