using MediatR;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Content.Commands;

public class GetContentTypeCommand : IRequest<ContentType>
{
    public Guid Id { get; set; }
}