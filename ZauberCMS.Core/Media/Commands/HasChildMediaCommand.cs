using MediatR;

namespace ZauberCMS.Core.Media.Commands;

public class HasChildMediaCommand : IRequest<bool>
{
    public Guid ParentId { get; set; }
}