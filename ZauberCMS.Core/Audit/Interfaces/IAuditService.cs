using System.Threading;
using System.Threading.Tasks;
using ZauberCMS.Core.Audit.Parameters;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Audit.Interfaces;

public interface IAuditService
{
    Task<HandlerResult<Models.Audit>> SaveAuditAsync(SaveAuditParameters parameters, CancellationToken cancellationToken = default);
    Task<PaginatedList<Models.Audit>> QueryAuditsAsync(QueryAuditsParameters parameters, CancellationToken cancellationToken = default);
    Task<HandlerResult<int>> CleanupOldAuditsAsync(CleanupOldAuditsParameters parameters, CancellationToken cancellationToken = default);
}