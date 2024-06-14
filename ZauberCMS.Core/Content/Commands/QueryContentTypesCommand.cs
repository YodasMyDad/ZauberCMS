using System.Linq.Expressions;
using MediatR;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Content.Commands;

public class QueryContentTypesCommand : IRequest<PaginatedList<ContentType>>
{
    public bool? ElementTypesOnly { get; set; }
    public bool RootOnly { get; set; }
    public bool AsNoTracking { get; set; } = true;
    public List<Guid> Ids { get; set; } = [];
    public int PageIndex { get; set; } = 1;
    public int AmountPerPage { get; set; }
    public string? SearchTerm { get; set; }
    public GetContentTypesOrderBy OrderBy { get; set; } = GetContentTypesOrderBy.DateUpdatedDescending;
    public Expression<Func<ContentType, bool>>? WhereClause { get; set; }
    
    public IQueryable<ContentType>? Query { get; set; }
}

public enum GetContentTypesOrderBy
{
    DateUpdated,
    DateUpdatedDescending,
    DateCreated,
    DateCreatedDescending,
    Name
}