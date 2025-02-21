using MediatR;
using ZauberCMS.Core.Seo.Models;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Seo.Commands;

public class DeleteRedirectCommand : IRequest<HandlerResult<SeoRedirect?>>
{
    public Guid? Id { get; set; }
}