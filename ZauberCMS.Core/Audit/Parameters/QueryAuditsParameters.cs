using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Audit.Parameters;

public class QueryAuditsParameters
{
    public bool AsNoTracking { get; set; } = true;
    public int PageIndex { get; set; } = 1;
    public int AmountPerPage { get; set; } = 10;
    public string? Username { get; set; }
    public GetAuditsOrderBy OrderBy { get; set; } = GetAuditsOrderBy.DateCreatedDescending;
    public Expression<Func<Models.Audit, bool>>? WhereClause { get; set; }
    public Func<IQueryable<Models.Audit>>? Query { get; set; }
}

public enum GetAuditsOrderBy
{
    DateCreated,
    DateCreatedDescending,
    Username
}