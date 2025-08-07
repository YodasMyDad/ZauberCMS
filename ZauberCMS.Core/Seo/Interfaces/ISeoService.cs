using ZauberCMS.Core.Seo.Models;
using ZauberCMS.Core.Seo.Parameters;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Seo.Interfaces;

public interface ISeoService
{
    Task<HandlerResult<Redirect>> SaveRedirectAsync(SaveRedirectParameters parameters, CancellationToken cancellationToken = default);
    Task<HandlerResult<Redirect>> QueryRedirectsAsync(QueryRedirectsParameters parameters, CancellationToken cancellationToken = default);
    Task<HandlerResult<Redirect>> DeleteRedirectAsync(DeleteRedirectParameters parameters, CancellationToken cancellationToken = default);
}
