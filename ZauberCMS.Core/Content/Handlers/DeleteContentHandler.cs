using MediatR;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Content.Commands;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Content.Handlers;

public class DeleteContentHandler(IServiceProvider serviceProvider) : IRequestHandler<DeleteContentCommand, HandlerResult<Models.Content>>
{
    public async Task<HandlerResult<Models.Content>> Handle(DeleteContentCommand request, CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ZauberDbContext>();
        
        var handlerResult = new HandlerResult<Models.Content>();
        
        var content = dbContext.Content.FirstOrDefault(x => x.Id == request.ContentId);
        if (content != null)
        {
            dbContext.Content.Remove(content);
            return await dbContext.SaveChangesAndLog(content, handlerResult, cancellationToken);
        }

        handlerResult.AddMessage("Unable to delete, as no Content with that id exists", ResultMessageType.Warning);
        return handlerResult;
    }
}