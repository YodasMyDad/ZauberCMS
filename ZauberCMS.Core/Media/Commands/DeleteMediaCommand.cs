using MediatR;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Media.Commands;

public class DeleteMediaCommand :  IRequest<HandlerResult<Models.Media>>
{
    public Guid MediaId { get; set; }
    public bool DeleteFile { get; set; }
}