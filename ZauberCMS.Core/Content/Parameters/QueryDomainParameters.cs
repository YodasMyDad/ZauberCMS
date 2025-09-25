using System.Linq.Expressions;
using ZauberCMS.Core.Content.Models;

namespace ZauberCMS.Core.Content.Parameters;

public class QueryDomainParameters
{
    public bool AsNoTracking { get; set; } = true;
    public List<Guid> Ids { get; set; } = [];
    public int PageIndex { get; set; } = 1;
    public int AmountPerPage { get; set; } = 10;
    public bool IncludeChildren { get; set; }
    public Guid? ContentId { get; set; }
    public Guid? LanguageId { get; set; }
    public GetDomainOrderBy OrderBy { get; set; } = GetDomainOrderBy.DateCreatedDescending;
    public Expression<Func<Domain, bool>>? WhereClause { get; set; }
    public Func<IQueryable<Domain>>? Query { get; set; }
}

public enum GetDomainOrderBy
{
    DateCreated,
    DateCreatedDescending,
    Url
}