using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Audit.Interfaces;
using ZauberCMS.Core.Audit.Models;
using ZauberCMS.Core.Audit.Parameters;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Plugins;
using ZauberCMS.Core.Shared.Models;
using ZauberCMS.Core.Shared.Services;

namespace ZauberCMS.Core.Audit.Services;

public class AuditService(
    IServiceProvider serviceProvider,
    ICacheService cacheService,
    ExtensionManager extensionManager) : IAuditService
{
    public async Task<HandlerResult<Audit>> SaveAuditAsync(SaveAuditParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var handlerResult = new HandlerResult<Audit>();

        if (parameters.Audit != null)
        {
            dbContext.Audits.Add(parameters.Audit);
            return await dbContext.SaveChangesAndLog(parameters.Audit, handlerResult, cacheService, extensionManager, cancellationToken);
        }

        handlerResult.AddMessage("Audit is null", ResultMessageType.Error);
        return handlerResult;
    }

    public async Task<HandlerResult<Audit>> QueryAuditsAsync(QueryAuditsParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var query = dbContext.Audits.AsQueryable();

        if (parameters.AsNoTracking)
        {
            query = query.AsNoTracking();
        }

        if (!parameters.SearchTerm.IsNullOrWhiteSpace())
        {
            query = query.Where(x => x.TableName.Contains(parameters.SearchTerm) || 
                                     x.Action.Contains(parameters.SearchTerm));
        }

        if (parameters.WhereClause != null)
        {
            query = query.Where(parameters.WhereClause);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        if (!parameters.OrderBy.IsNullOrWhiteSpace())
        {
            query = query.OrderBy(parameters.OrderBy);
        }
        else
        {
            query = query.OrderByDescending(x => x.DateCreated);
        }

        if (parameters.AmountPerPage > 0)
        {
            query = query.Skip(parameters.PageIndex * parameters.AmountPerPage).Take(parameters.AmountPerPage);
        }

        var items = await query.ToListAsync(cancellationToken);

        return new HandlerResult<Audit>
        {
            Success = true,
            Items = items,
            TotalCount = totalCount
        };
    }

    public async Task<HandlerResult<Audit>> CleanupOldAuditsAsync(CleanupOldAuditsParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var handlerResult = new HandlerResult<Audit>();

        var cutoffDate = DateTime.UtcNow.AddDays(-parameters.DaysToKeep);
        var auditsToDelete = dbContext.Audits.Where(x => x.DateCreated < cutoffDate);

        var deletedCount = await auditsToDelete.CountAsync(cancellationToken);
        dbContext.Audits.RemoveRange(auditsToDelete);
        await dbContext.SaveChangesAsync(cancellationToken);

        handlerResult.Messages.Add(new ResultMessage($"Deleted {deletedCount} old audit records", ResultMessageType.Success));
        handlerResult.Success = true;

        return handlerResult;
    }
}