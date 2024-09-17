using AutoMapper;
using MediatR;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Membership.Commands;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Shared.Models;
using ZauberCMS.Core.Shared.Services;

namespace ZauberCMS.Core.Membership.Handlers;

public class SaveRoleHandler(
    IServiceProvider serviceProvider,
    IMediator mediator,
    IMapper mapper,
    ICacheService cacheService,
    AuthenticationStateProvider authenticationStateProvider)
    : IRequestHandler<SaveRoleCommand, HandlerResult<Role>>
{
    public async Task<HandlerResult<Role>> Handle(SaveRoleCommand request, CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ZauberDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var authState = await authenticationStateProvider.GetAuthenticationStateAsync();
        var loggedInUser = await userManager.GetUserAsync(authState.User);
        var handlerResult = new HandlerResult<Role>();
        var isUpdate = false;
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
                isUpdate = true;
                // Map the updated properties
                mapper.Map(request.Role, role);
                role.DateUpdated = DateTime.UtcNow;
            }
            
            await loggedInUser.AddAudit(role, role.Name, isUpdate ? AuditExtensions.AuditAction.Update : AuditExtensions.AuditAction.Create, mediator, cancellationToken);
            return await dbContext.SaveChangesAndLog(role, handlerResult, cacheService, cancellationToken);
        }

        handlerResult.AddMessage("Role is null", ResultMessageType.Error);
        return handlerResult;
    }
}