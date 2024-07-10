using MediatR;

namespace ZauberCMS.Core.Content.Commands;

public class HasChildContentCommand : IRequest<bool>
{
    public Guid ParentId { get; set; }
}