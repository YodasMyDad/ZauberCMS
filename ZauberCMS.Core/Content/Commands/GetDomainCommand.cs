using MediatR;
using ZauberCMS.Core.Content.Models;

namespace ZauberCMS.Core.Content.Commands;

public class GetDomainCommand : IRequest<Domain>
{
    public Guid? Id { get; set; }
    public bool AsNoTracking { get; set; }
    public string? Url { get; set; } 
}