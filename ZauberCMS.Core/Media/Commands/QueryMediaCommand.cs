using System.Linq.Expressions;
using MediatR;
using ZauberCMS.Core.Media.Models;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Media.Commands;

public class QueryMediaCommand : IRequest<PaginatedList<Models.Media>>
{
    public bool AsNoTracking { get; set; } = true;
    public List<Guid> Ids { get; set; } = [];
    public int PageIndex { get; set; } = 1;
    public int AmountPerPage { get; set; } = 10;
    public bool IncludeChildren { get; set; }
    public List<MediaType> MediaTypes { get; set; } = [];
    public GetMediaOrderBy OrderBy { get; set; } = GetMediaOrderBy.DateUpdatedDescending;
    public Expression<Func<Models.Media, bool>>? WhereClause { get; set; }
    public IQueryable<Models.Media>? Query { get; set; }
}

public enum GetMediaOrderBy
{
    DateUpdated,
    DateUpdatedDescending,
    DateCreated,
    DateCreatedDescending,
    Name,
    NameDescending
}