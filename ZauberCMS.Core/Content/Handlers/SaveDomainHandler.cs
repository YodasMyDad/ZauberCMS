using AutoMapper;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Content.Commands;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Content.Handlers;

public class SaveDomainHandler(
    IServiceProvider serviceProvider,
    IMapper mapper)
    : IRequestHandler<SaveDomainCommand, HandlerResult<Domain>>
{
    public async Task<HandlerResult<Domain>> Handle(SaveDomainCommand request, CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ZauberDbContext>();

        var handlerResult = new HandlerResult<Domain>();

        if (request.Domain != null)
        {
            // Get the DB version
            var domain = dbContext.Domains
                .FirstOrDefault(x => x.Id == request.Domain.Id);

            if (domain == null)
            {
                domain = request.Domain;
                dbContext.Domains.Add(domain);
            }
            else
            {
                // Map the updated properties
                mapper.Map(request.Domain, domain);
            }

            return await dbContext.SaveChangesAndLog(domain, handlerResult, cancellationToken);
        }

        handlerResult.AddMessage("Domain is null", ResultMessageType.Error);
        return handlerResult;
    }
}