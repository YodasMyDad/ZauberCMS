﻿using AutoMapper;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Data.Commands;
using ZauberCMS.Core.Data.Models;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Plugins;
using ZauberCMS.Core.Shared.Models;
using ZauberCMS.Core.Shared.Services;

namespace ZauberCMS.Core.Data.Handlers;

public class SaveGlobalDataHandler(
    IServiceProvider serviceProvider,
    ICacheService cacheService,
    ExtensionManager extensionManager)
    : IRequestHandler<SaveGlobalDataCommand, HandlerResult<GlobalData>>
{
    public async Task<HandlerResult<GlobalData>> Handle(SaveGlobalDataCommand request, CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ZauberDbContext>();
        
        var handlerResult = new HandlerResult<GlobalData>();

        if (!request.Alias.IsNullOrWhiteSpace() && !request.Data.IsNullOrWhiteSpace())
        {

            // Get the DB version
            var gData = dbContext.GlobalDatas
                .FirstOrDefault(x => x.Alias == request.Alias);

            if (gData == null)
            {
                gData = new GlobalData{Alias = request.Alias, Data = request.Data};
                dbContext.GlobalDatas.Add(gData);
            }
            else
            {
                gData.Data = request.Data;   
                gData.DateUpdated = DateTime.UtcNow;
            }
            
            return await dbContext.SaveChangesAndLog(gData, handlerResult, cacheService, extensionManager, cancellationToken);
        }

        handlerResult.AddMessage("GlobalData is null", ResultMessageType.Error);
        return handlerResult;
    }
}