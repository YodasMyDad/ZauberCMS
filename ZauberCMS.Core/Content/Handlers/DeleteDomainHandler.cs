using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Content.Commands;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Content.Handlers;

public class DeleteDomainHandler(IServiceProvider serviceProvider) : IRequestHandler<DeleteDomainCommand, HandlerResult<Domain?>>
{
    public async Task<HandlerResult<Domain?>> Handle(DeleteDomainCommand request, CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ZauberDbContext>();
        
        var handlerResult = new HandlerResult<Domain>();

        if (request.Id != null)
        {
            var domain = await dbContext.Domains.FirstOrDefaultAsync(l => l.Id == request.Id, cancellationToken: cancellationToken);
            if (domain != null)
            {
                dbContext.Domains.Remove(domain);
            }
        }
        else
        {
            var domain = await dbContext.Domains.FirstOrDefaultAsync(l => l.ContentId == request.ContentId, cancellationToken: cancellationToken);
            if (domain != null)
            {
                dbContext.Domains.Remove(domain);
            }
        }
        
        return (await dbContext.SaveChangesAndLog(null, handlerResult, cancellationToken))!;
    }
}