using AutoMapper;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Membership.Commands;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Membership.Handlers;

public class SaveRoleHandler(
    IServiceProvider serviceProvider,
    IMapper mapper)
    : IRequestHandler<SaveRoleCommand, HandlerResult<Role>>
{

    
    public async Task<HandlerResult<Role>> Handle(SaveRoleCommand request, CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ZauberDbContext>();
        
        var handlerResult = new HandlerResult<Role>();

        if (request.Role != null)
        {

            // Get the DB version
            var role = dbContext.Roles
                .FirstOrDefault(x => x.Id == request.Role.Id);

            if (role == null)
            {
                role = request.Role;
                dbContext.Roles.Add(role);
            }
            else
            {
                // Map the updated properties
                mapper.Map(request.Role, role);   
            }
            
            return await dbContext.SaveChangesAndLog(role, handlerResult, cancellationToken);
        }

        handlerResult.AddMessage("Role is null", ResultMessageType.Error);
        return handlerResult;
    }
}