using ZauberCMS.Core.Audit.Models;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Audit.Interfaces;

public interface IAuditService
{
    Task<HandlerResult<Audit>> SaveAuditAsync(SaveAuditParameters parameters, CancellationToken cancellationToken = default);
    Task<HandlerResult<Audit>> QueryAuditsAsync(QueryAuditsParameters parameters, CancellationToken cancellationToken = default);
    Task<HandlerResult<Audit>> CleanupOldAuditsAsync(CleanupOldAuditsParameters parameters, CancellationToken cancellationToken = default);
}
