using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace ZauberCMS.Core.Content.Parameters;

public class QueryContentParameters : BaseQueryContentParameters
{
    public bool Cached { get; set; }
    public List<Guid> Ids { get; set; } = [];
    public List<string> TagSlugs { get; set; } = [];
    public int PageIndex { get; set; } = 1;
    public int AmountPerPage { get; set; } = 10;
    public string? SearchTerm { get; set; }
    public bool IncludeUnpublished { get; set; }
    public bool IncludeContentRoles { get; set; }
    public bool OnlyUnpublished { get; set; }
    public bool? IsDeleted { get; set; } = false;
    public bool RootContentOnly { get; set; }
    public Expression<Func<Content.Models.Content, bool>>? WhereClause { get; set; }
    public Func<IQueryable<Content.Models.Content>>? Query { get; set; }
}

public class BaseQueryContentParameters
{
    public string ContentTypeAlias { get; set; } = string.Empty;
    public Guid? ContentTypeId { get; set; }
    public bool AsNoTracking { get; set; } = true;
    public bool IncludeChildren { get; set; }
    public Guid? ParentId { get; set; }
    public Guid? LastEditedBy { get; set; }
    public enum NestedContentFilter { Include, Exclude, Only }
    public NestedContentFilter NestedFilter { get; set; } = NestedContentFilter.Exclude;
    /*public enum PublishedContentFilter { OnlyPublished, IncludeUnpublished, OnlyUnpublished }
    public PublishedContentFilter PublishedFilter { get; set; } = PublishedContentFilter.OnlyPublished;*/

    public GetContentsOrderBy OrderBy { get; set; } = GetContentsOrderBy.DateUpdatedDescending;
}

public enum GetContentsOrderBy
{
    DateUpdated,
    DateUpdatedDescending,
    DateCreated,
    DateCreatedDescending,
    SortOrder
}