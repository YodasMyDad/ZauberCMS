using MediatR;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Media.Commands;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Media.Handlers;

public class DeleteMediaHandler(IServiceProvider serviceProvider) : IRequestHandler<DeleteMediaCommand, HandlerResult<Models.Media>>
{
    public async Task<HandlerResult<Models.Media>> Handle(DeleteMediaCommand request, CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ZauberDbContext>();
        
        var handlerResult = new HandlerResult<Models.Media>();
        
        var media = dbContext.Media.FirstOrDefault(x => x.Id == request.MediaId);
        if (media != null)
        {
            dbContext.Media.Remove(media);
            return await dbContext.SaveChangesAndLog(media, handlerResult, cancellationToken);
        }

        handlerResult.AddMessage("Unable to delete, as no Media with that id exists", ResultMessageType.Warning);
        return handlerResult;
    }
}