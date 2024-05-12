using MediatR;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Content.Commands;

public class GetContentTypesCommand : IRequest<PaginatedList<ContentType>>
{
    public GetContentTypesCommand()
    {
    }
    
    public GetContentTypesCommand(int amountPerPage)
    {
        AmountPerPage = amountPerPage;
    }
    
    public bool AsNoTracking { get; set; } = true;
    public List<Guid> CategoryTypeIds { get; set; } = [];
    public int PageIndex { get; set; } = 1;
    public int AmountPerPage { get; set; }
    public string? SearchTerm { get; set; }
    public GetContentTypesOrderBy OrderBy { get; set; } = GetContentTypesOrderBy.DateUpdatedDescending;
}

public enum GetContentTypesOrderBy
{
    DateUpdated,
    DateUpdatedDescending,
    DateCreated,
    DateCreatedDescending
}