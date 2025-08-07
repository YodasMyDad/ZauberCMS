using ZauberCMS.Core.Seo.Models;

namespace ZauberCMS.Core.Seo.Parameters;

public class QueryRedirectsParameters
{
    /// <summary>
    /// Whether this query is to be cached
    /// </summary>
    public bool Cached { get; set; } = true;
    public bool AsNoTracking { get; set; } = true;
    public List<Guid> Ids { get; set; } = [];
    public int Amount { get; set; } = 5000;
    public GetSeoRedirectOrderBy OrderBy { get; set; } = GetSeoRedirectOrderBy.DateUpdatedDescending;
    public Func<IQueryable<SeoRedirect>>? Query { get; set; }
}

public enum GetSeoRedirectOrderBy
{
    DateCreated,
    DateCreatedDescending,
    DateUpdated,
    DateUpdatedDescending,
    FromUrl
}