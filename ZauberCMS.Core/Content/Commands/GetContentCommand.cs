using MediatR;

namespace ZauberCMS.Core.Content.Commands;

public class GetContentCommand : IRequest<Models.Content>
{
    public Guid Id { get; set; }
}