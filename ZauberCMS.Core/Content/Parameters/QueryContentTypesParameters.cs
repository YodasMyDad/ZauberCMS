using System.Linq.Expressions;
using ZauberCMS.Core.Content.Models;

namespace ZauberCMS.Core.Content.Parameters;

public class QueryContentTypesParameters
{
    public bool IncludeElementTypes { get; set; }
    public bool OnlyElementTypes { get; set; }
    public bool OnlyCompositions { get; set; }
    public bool IncludeCompositions { get; set; }
    public bool RootOnly { get; set; }
    public bool AsNoTracking { get; set; } = true;
    public bool IncludeFolders { get; set; }
    public bool OnlyFolders { get; set; }
    public List<Guid> Ids { get; set; } = [];
    public int PageIndex { get; set; } = 1;
    public int AmountPerPage { get; set; } = 10;
    public Guid? ParentId { get; set; }
    public string? SearchTerm { get; set; }
    public GetContentTypesOrderBy OrderBy { get; set; } = GetContentTypesOrderBy.DateUpdatedDescending;
    public Expression<Func<ContentType, bool>>? WhereClause { get; set; }
    public Func<IQueryable<ContentType>>? Query { get; set; }
}

public enum GetContentTypesOrderBy
{
    DateUpdated,
    DateUpdatedDescending,
    DateCreated,
    DateCreatedDescending,
    Name
}