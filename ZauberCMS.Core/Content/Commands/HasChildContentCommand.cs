using MediatR;

namespace ZauberCMS.Core.Content.Commands;

public class HasChildContentCommand : IRequest<bool>
{
    public bool Cached { get; set; } = true;
    public Guid ParentId { get; set; }
}