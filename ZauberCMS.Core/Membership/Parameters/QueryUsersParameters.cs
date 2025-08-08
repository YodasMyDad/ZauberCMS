using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Membership.Parameters;

public class QueryUsersParameters
{
    public bool Cached { get; set; }
    public List<string> Roles { get; set; } = [];
    public bool AsNoTracking { get; set; } = true;
    public List<Guid> Ids { get; set; } = [];
    public int PageIndex { get; set; } = 1;
    public int AmountPerPage { get; set; } = 10;
    public string? SearchTerm { get; set; }
    public GetUsersOrderBy OrderBy { get; set; } = GetUsersOrderBy.DateUpdatedDescending;
    public Expression<Func<User, bool>>? WhereClause { get; set; }
    public Func<IQueryable<User>>? Query { get; set; }
}

public enum GetUsersOrderBy
{
    DateUpdated,
    DateUpdatedDescending,
    DateCreated,
    DateCreatedDescending
}