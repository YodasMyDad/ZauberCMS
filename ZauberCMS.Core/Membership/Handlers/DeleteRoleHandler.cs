﻿using AutoMapper;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Membership.Commands;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Membership.Handlers;

public class DeleteRoleHandler(IServiceProvider serviceProvider, IMapper mapper, IMediator mediator)
    : IRequestHandler<DeleteRoleCommand, HandlerResult<Role>>
{

    public async Task<HandlerResult<Role>> Handle(DeleteRoleCommand request, CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ZauberDbContext>();
        
        var handlerResult = new HandlerResult<Role>();
        
        var role = await dbContext.Roles
            .Include(r => r.UserRoles) // Include UserRoles to delete related user-role relationships
            .FirstOrDefaultAsync(x => x.Id == request.RoleId, cancellationToken);

        if (role != null)
        {
            var usersInThisRole = await mediator.Send(new QueryUsersCommand { Roles = [role.Name!] }, cancellationToken);
            if (usersInThisRole.Items.Any())
            {
                // Display error message
                handlerResult.Messages.Add(new ResultMessage("Unable to delete as users are in this role", ResultMessageType.Error));
                return handlerResult;
            }
            
            dbContext.Roles.Remove(role);
            await dbContext.SaveChangesAsync(cancellationToken);
            handlerResult.Messages.Add(new ResultMessage("Role deleted successfully", ResultMessageType.Success));
            handlerResult.Success = true;
        }
        else
        {
            handlerResult.AddMessage("Role not found", ResultMessageType.Error);
        }

        return handlerResult;
    }
}