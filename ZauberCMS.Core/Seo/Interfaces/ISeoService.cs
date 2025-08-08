using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ZauberCMS.Core.Seo.Models;
using ZauberCMS.Core.Seo.Parameters;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Seo.Interfaces;

public interface ISeoService
{
    Task<HandlerResult<SeoRedirect>> SaveRedirectAsync(SaveRedirectParameters parameters, CancellationToken cancellationToken = default);
    Task<List<SeoRedirect>> QueryRedirectsAsync(QueryRedirectsParameters parameters, CancellationToken cancellationToken = default);
    Task<HandlerResult<SeoRedirect?>> DeleteRedirectAsync(DeleteRedirectParameters parameters, CancellationToken cancellationToken = default);
}