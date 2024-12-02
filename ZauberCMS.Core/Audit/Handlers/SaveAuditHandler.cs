﻿using AutoMapper;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Audit.Commands;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Plugins;
using ZauberCMS.Core.Shared.Models;
using ZauberCMS.Core.Shared.Services;

namespace ZauberCMS.Core.Audit.Handlers;

public class SaveAuditHandler(
    IServiceProvider serviceProvider,
    ICacheService cacheService,
    IMapper mapper,
    ExtensionManager extensionManager)
    : IRequestHandler<SaveAuditCommand, HandlerResult<Models.Audit>>
{

    
    public async Task<HandlerResult<Models.Audit>> Handle(SaveAuditCommand request, CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ZauberDbContext>();
        
        var handlerResult = new HandlerResult<Models.Audit>();

        if (request.Audit != null)
        {

            // Get the DB version
            var audit = dbContext.Audits
                .FirstOrDefault(x => x.Id == request.Audit.Id);

            if (audit == null)
            {
                audit = request.Audit;
                dbContext.Audits.Add(audit);
            }
            else
            {
                // Map the updated properties
                mapper.Map(request.Audit, audit);
                audit.DateUpdated = DateTime.UtcNow;                
            }
            
            return await dbContext.SaveChangesAndLog(audit, handlerResult, cacheService, extensionManager, cancellationToken);
        }

        handlerResult.AddMessage("Audit is null", ResultMessageType.Error);
        return handlerResult;
    }
}