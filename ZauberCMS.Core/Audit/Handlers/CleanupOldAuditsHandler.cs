
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZauberCMS.Core.Audit.Commands;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Plugins;
using ZauberCMS.Core.Shared.Models;
using ZauberCMS.Core.Shared.Services;

namespace ZauberCMS.Core.Audit.Handlers;

public class CleanupOldAuditsHandler(
    IServiceProvider serviceProvider,
    ILogger<CleanupOldAuditsHandler> logger)
    : IRequestHandler<CleanupOldAuditsCommand, HandlerResult<int>>
{
    public async Task<HandlerResult<int>> Handle(CleanupOldAuditsCommand request, CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var handlerResult = new HandlerResult<int>();

        try
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-request.DaysToKeep);
            
            var oldAudits = dbContext.Audits
                .Where(a => a.DateCreated < cutoffDate);
            
            var auditCount = await oldAudits.CountAsync(cancellationToken);
            
            if (auditCount > 0)
            {
                logger.LogInformation("Deleting {AuditCount} audit records older than {DaysToKeep} days", auditCount, request.DaysToKeep);
                
                dbContext.RemoveRange(oldAudits);
                await dbContext.SaveChangesAsync(cancellationToken);
                
                logger.LogInformation("Successfully deleted {AuditCount} old audit records", auditCount);
                
                handlerResult.Entity = auditCount;
                handlerResult.AddMessage($"Successfully deleted {auditCount} audit records older than {request.DaysToKeep} days", ResultMessageType.Success);
            }
            else
            {
                logger.LogInformation("No audit records older than {DaysToKeep} days found", request.DaysToKeep);
                handlerResult.Entity = 0;
                handlerResult.AddMessage($"No audit records older than {request.DaysToKeep} days found", ResultMessageType.Info);
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