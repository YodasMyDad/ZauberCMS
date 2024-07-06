using System.Linq.Expressions;
using MediatR;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Content.Commands;

public class QueryContentCommand : IRequest<PaginatedList<Models.Content>>
{
    public string ContentTypeAlias { get; set; } = string.Empty;
    public Guid? ContentTypeId { get; set; }
    public bool AsNoTracking { get; set; } = true;
    public List<Guid> Ids { get; set; } = [];
    public int PageIndex { get; set; } = 1;
    public int AmountPerPage { get; set; } = 10;
    public string? SearchTerm { get; set; }
    public bool IncludeChildren { get; set; }
    public GetContentsOrderBy OrderBy { get; set; } = GetContentsOrderBy.DateUpdatedDescending;
    public Expression<Func<Models.Content, bool>>? WhereClause { get; set; }
    public IQueryable<Models.Content>? Query { get; set; }
}

public enum GetContentsOrderBy
{
    DateUpdated,
    DateUpdatedDescending,
    DateCreated,
    DateCreatedDescending,
    SortOrder
}