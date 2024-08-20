using System.Linq.Expressions;
using MediatR;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Content.Commands;

public class QueryDomainCommand : IRequest<PaginatedList<Domain>>
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
    public IQueryable<Domain>? Query { get; set; }
}

public enum GetDomainOrderBy
{
    DateCreated,
    DateCreatedDescending,
    Url
}