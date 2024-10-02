using System.Linq.Expressions;
using MediatR;
using ZauberCMS.Core.Shared.Models;
using ZauberCMS.Core.Tags.Models;

namespace ZauberCMS.Core.Tags.Commands;

public class QueryTagCommand : IRequest<PaginatedList<Tag>>
{
    public bool Cached {get;set;}
    public bool AsNoTracking { get; set; } = true;
    
    public List<Guid> Ids { get; set; } = [];
    public List<string> TagNames { get; set; } = [];
    public List<string> TagSlugs { get; set; } = [];
    public List<Guid> ItemIds { get; set; } = [];
    
    public int PageIndex { get; set; } = 1;
    public int AmountPerPage { get; set; } = 10;
    public GetTagOrderBy OrderBy { get; set; } = GetTagOrderBy.TagName;
    public Expression<Func<Tag, bool>>? WhereClause { get; set; }
    public IQueryable<Tag>? Query { get; set; }
}

public enum GetTagOrderBy
{
    DateCreated,
    DateCreatedDescending,
    TagName,
    TagNameDescending,
    SortOrder
}