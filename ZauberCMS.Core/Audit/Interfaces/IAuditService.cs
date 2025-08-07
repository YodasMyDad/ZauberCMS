using ZauberCMS.Core.Audit.Models;
using ZauberCMS.Core.Audit.Parameters;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Audit.Interfaces;

public interface IAuditService
{
    Task<HandlerResult<Audit>> SaveAuditAsync(SaveAuditParameters parameters, CancellationToken cancellationToken = default);
    Task<PaginatedList<Audit>> QueryAuditsAsync(QueryAuditsParameters parameters, CancellationToken cancellationToken = default);
    Task<HandlerResult<int>> CleanupOldAuditsAsync(CleanupOldAuditsParameters parameters, CancellationToken cancellationToken = default);
}
