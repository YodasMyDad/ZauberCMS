using MediatR;
using ZauberCMS.Core.Seo.Models;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Seo.Commands;

public class SaveRedirectCommand : IRequest<HandlerResult<SeoRedirect>>
{
    public SeoRedirect? Redirect { get; set; }
}