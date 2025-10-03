using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZauberCMS.Core.Audit.Interfaces;
using ZauberCMS.Core.Audit.Parameters;
using ZauberCMS.Core.Audit.Mapping;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Plugins;
using ZauberCMS.Core.Shared.Models;
using ZauberCMS.Core.Shared.Services;

namespace ZauberCMS.Core.Audit.Services;

public class AuditService(
    IServiceScopeFactory serviceScopeFactory,
    ICacheService cacheService,
    ExtensionManager extensionManager,
    ILogger<AuditService> logger)
    : IAuditService
{
    /// <summary>
    /// Creates or updates an audit record and persists it.
    /// </summary>
    /// <param name="parameters">Audit model to save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result including success and messages.</returns>
    public async Task<HandlerResult<Models.Audit>> SaveAuditAsync(SaveAuditParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        
        var handlerResult = new HandlerResult<Models.Audit>();

        if (parameters.Audit != null)
        {
            // Get the DB version
            var audit = dbContext.Audits
                .FirstOrDefault(x => x.Id == parameters.Audit.Id);

            if (audit == null)
            {
                audit = parameters.Audit;
                dbContext.Audits.Add(audit);
            }
            else
            {
                parameters.Audit.MapTo(audit);
                audit.DateUpdated = DateTime.UtcNow;                
            }
            
            return await dbContext.SaveChangesAndLog(audit, handlerResult, cacheService, extensionManager, cancellationToken);
        }

        handlerResult.AddMessage("Audit is null", ResultMessageType.Error);
        return handlerResult;
    }

    /// <summary>
    /// Queries audits with optional where clause and ordering; returns paged list.
    /// </summary>
    /// <param name="parameters">Query options including paging.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Paged list of audits.</returns>
    public Task<PaginatedList<Models.Audit>> QueryAuditsAsync(QueryAuditsParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var query = dbContext.Audits.AsQueryable();

        if (parameters.Query != null)
        {
            query = parameters.Query.Invoke();
        }
        else
        {
            if (parameters.AsNoTracking)
            {
                query = query.AsNoTracking();
            }
        }

        /*if (parameters.Username != null)
        {
            query = query.Where(x => x.Username == parameters.Username);
        }*/
        
        if (parameters.WhereClause != null)
        {
            query = query.Where(parameters.WhereClause);
        }   
        
        
        query = parameters.OrderBy switch
        {
            GetAuditsOrderBy.DateCreated => query.OrderBy(p => p.DateCreated),
            GetAuditsOrderBy.DateCreatedDescending => query.OrderByDescending(p => p.DateCreated),
            _ => query.OrderByDescending(p => p.DateCreated)
        };
        
        return Task.FromResult(query.ToPaginatedList(parameters.PageIndex, parameters.AmountPerPage));
    }

    /// <summary>
    /// Deletes audit records older than the specified retention window and returns the count removed.
    /// </summary>
    /// <param name="parameters">Number of days to keep.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing the number of deleted rows.</returns>
    public async Task<HandlerResult<int>> CleanupOldAuditsAsync(CleanupOldAuditsParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var handlerResult = new HandlerResult<int>();

        try
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-parameters.DaysToKeep);
            
            var oldAudits = dbContext.Audits
                .Where(a => a.DateCreated < cutoffDate);
            
            var auditCount = await oldAudits.CountAsync(cancellationToken);
            
            if (auditCount > 0)
            {
                logger.LogInformation("Deleting {AuditCount} audit records older than {DaysToKeep} days", auditCount, parameters.DaysToKeep);
                
                dbContext.RemoveRange(oldAudits);
                await dbContext.SaveChangesAsync(cancellationToken);
                
                logger.LogInformation("Successfully deleted {AuditCount} old audit records", auditCount);
                
                handlerResult.Entity = auditCount;
                handlerResult.AddMessage($"Successfully deleted {auditCount} audit records older than {parameters.DaysToKeep} days", ResultMessageType.Success);
            }
            else
            {
                logger.LogInformation("No audit records older than {DaysToKeep} days found", parameters.DaysToKeep);
                handlerResult.Entity = 0;
                handlerResult.AddMessage($"No audit records older than {parameters.DaysToKeep} days found", ResultMessageType.Info);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while cleaning up old audit records");
            handlerResult.AddMessage("An error occurred while cleaning up old audit records", ResultMessageType.Error);
        }

        return handlerResult;
    }
}
