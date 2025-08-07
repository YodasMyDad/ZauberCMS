using MediatR;
using ZauberCMS.Core.Audit.Commands;
using ZauberCMS.Core.Audit.Interfaces;
using ZauberCMS.Core.Audit.Models;
using ZauberCMS.Core.Audit.Parameters;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Audit.Services;

public class AuditService(IMediator mediator) : IAuditService
{
    public async Task<HandlerResult<Audit>> SaveAuditAsync(SaveAuditParameters parameters, CancellationToken cancellationToken = default)
    {
        var command = new SaveAuditCommand
        {
            Audit = parameters.Audit
        };
        
        return await mediator.Send(command, cancellationToken);
    }

    public async Task<HandlerResult<Audit>> QueryAuditsAsync(QueryAuditsParameters parameters, CancellationToken cancellationToken = default)
    {
        var command = new QueryAuditsCommand
        {
            AmountPerPage = parameters.AmountPerPage,
            PageIndex = parameters.PageIndex,
            OrderBy = parameters.OrderBy,
            WhereClause = parameters.WhereClause,
            AsNoTracking = parameters.AsNoTracking,
            SearchTerm = parameters.SearchTerm
        };
        
        return await mediator.Send(command, cancellationToken);
    }

    public async Task<HandlerResult<Audit>> CleanupOldAuditsAsync(CleanupOldAuditsParameters parameters, CancellationToken cancellationToken = default)
    {
        var command = new CleanupOldAuditsCommand
        {
            DaysToKeep = parameters.DaysToKeep
        };
        
        return await mediator.Send(command, cancellationToken);
    }
}