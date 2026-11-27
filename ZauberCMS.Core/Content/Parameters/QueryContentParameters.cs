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
    /// <summary>
    /// Also return items in the recycle bin. <c>false</c> (the default): only items not in the recycle bin. Use it
    /// only on purpose (maintenance, admin tools), never for pages shown to visitors. Included
    /// <see cref="BaseQueryContentParameters.IncludeChildren">children</see> never contain recycled items.
    /// </summary>
    public bool IncludeDeleted { get; set; }

    /// <summary>
    /// Return only items in the recycle bin (wins over <see cref="IncludeDeleted"/>). Included
    /// <see cref="BaseQueryContentParameters.IncludeChildren">children</see> never contain recycled items.
    /// </summary>
    public bool OnlyDeleted { get; set; }

    /// <summary>
    /// The v4 recycle-bin filter, kept as an alias (ZA-124): <c>false</c> = neither flag (the default), <c>true</c> =
    /// <see cref="OnlyDeleted"/>, <c>null</c> = <see cref="IncludeDeleted"/>.
    /// </summary>
    [Obsolete("Use IncludeDeleted (also recycled items) or OnlyDeleted (only recycled items), as the other parameter " +
              "classes do. Will be removed in a later major version.")]
    public bool? IsDeleted
    {
        get => OnlyDeleted ? true : IncludeDeleted ? null : false;
        set
        {
            OnlyDeleted = value == true;
            IncludeDeleted = value == null;
        }
    }
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