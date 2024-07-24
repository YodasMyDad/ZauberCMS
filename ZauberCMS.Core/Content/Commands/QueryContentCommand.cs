using System.Linq.Expressions;
using MediatR;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Content.Commands;

public class QueryContentCommand : BaseQueryContentCommand, IRequest<PaginatedList<Models.Content>>
{
    /// <summary>
    /// Whether this query is to be cached
    /// </summary>
    public bool Cached { get; set; } 
    
    /// <summary>
    /// Return content items by id
    /// </summary>
    public List<Guid> Ids { get; set; } = [];
    
    /// <summary>
    /// Current page to return
    /// </summary>
    public int PageIndex { get; set; } = 1;
    
    /// <summary>
    /// The amount of items to return
    /// </summary>
    public int AmountPerPage { get; set; } = 10;
    
    /// <summary>
    /// Return content that has this in the name
    /// </summary>
    public string? SearchTerm { get; set; }
    
    /// <summary>
    /// Where or not to include unpublished content in this query
    /// </summary>
    public bool IncludeUnpublished { get; set; }
    
    /// <summary>
    /// Where clause builder
    /// </summary>
    public Expression<Func<Models.Content, bool>>? WhereClause { get; set; }
    
    /// <summary>
    /// Optional direct query
    /// </summary>
    public IQueryable<Models.Content>? Query { get; set; }
}

public class BaseQueryContentCommand
{
    /// <summary>
    /// Return all items using this content type alias
    /// </summary>
    public string ContentTypeAlias { get; set; } = string.Empty;
    
    /// <summary>
    /// Return all items using this content type id
    /// </summary>
    public Guid? ContentTypeId { get; set; }
    
    /// <summary>
    /// Make the query AsNoTracking, true by default
    /// </summary>
    public bool AsNoTracking { get; set; } = true;
    
    /// <summary>
    /// Include all child items on the content you are querying
    /// </summary>
    public bool IncludeChildren { get; set; }
    
    /// <summary>
    /// Show items that have a parent id matching this
    /// </summary>
    public Guid? ParentId { get; set; }
    
    /// <summary>
    /// How to order the content
    /// </summary>
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