using MediatR;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Content.Commands;

public class GetContentsCommand : IRequest<PaginatedList<Models.Content>>
{
    public GetContentsCommand()
    {
    }
    
    public GetContentsCommand(int amountPerPage)
    {
        AmountPerPage = amountPerPage;
    }
    
    public bool AsNoTracking { get; set; } = true;
    
    public List<Guid> Ids { get; set; } = [];
    public int PageIndex { get; set; } = 1;
    public int AmountPerPage { get; set; }
    public string? SearchTerm { get; set; }
    public GetContentsOrderBy OrderBy { get; set; } = GetContentsOrderBy.DateUpdatedDescending;
}

public enum GetContentsOrderBy
{
    DateUpdated,
    DateUpdatedDescending,
    DateCreated,
    DateCreatedDescending
}