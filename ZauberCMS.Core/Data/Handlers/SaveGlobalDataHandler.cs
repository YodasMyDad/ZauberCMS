using AutoMapper;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Data.Commands;
using ZauberCMS.Core.Data.Models;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Shared.Models;
using ZauberCMS.Core.Shared.Services;

namespace ZauberCMS.Core.Data.Handlers;

public class SaveGlobalDataHandler(
    IServiceProvider serviceProvider,
    ICacheService cacheService,
    IMapper mapper)
    : IRequestHandler<SaveGlobalDataCommand, HandlerResult<GlobalData>>
{

    
    public async Task<HandlerResult<GlobalData>> Handle(SaveGlobalDataCommand request, CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ZauberDbContext>();
        
        var handlerResult = new HandlerResult<GlobalData>();

        if (request.GlobalData != null)
        {

            // Get the DB version
            var gData = dbContext.GlobalDatas
                .FirstOrDefault(x => x.Id == request.GlobalData.Id);

            if (gData == null)
            {
                gData = request.GlobalData;
                dbContext.GlobalDatas.Add(gData);
            }
            else
            {
                // Map the updated properties
                mapper.Map(request.GlobalData, gData);   
                gData.DateUpdated = DateTime.UtcNow;
            }
            
            return await dbContext.SaveChangesAndLog(gData, handlerResult, cacheService, cancellationToken);
        }

        handlerResult.AddMessage("GlobalData is null", ResultMessageType.Error);
        return handlerResult;
    }
}