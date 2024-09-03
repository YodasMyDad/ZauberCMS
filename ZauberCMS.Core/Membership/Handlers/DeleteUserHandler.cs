using MediatR;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Membership.Commands;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Membership.Handlers;

public class DeleteUserHandler(
    IServiceProvider serviceProvider,
    IMediator mediator,
    AuthenticationStateProvider authenticationStateProvider)
    : IRequestHandler<DeleteUserCommand, HandlerResult<User>>
{
    public async Task<HandlerResult<User>> Handle(DeleteUserCommand request, CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ZauberDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var authState = await authenticationStateProvider.GetAuthenticationStateAsync();
        var loggedInUser = await userManager.GetUserAsync(authState.User);
        var handlerResult = new HandlerResult<User>();

        var user = await dbContext.Users
            .Include(u => u.UserRoles) // Include UserRoles to delete related roles
            .FirstOrDefaultAsync(x => x.Id == request.UserId, cancellationToken);

        if (user != null)
        {
            // Now delete the PropertyData
            var propertyDataToDelete = dbContext.UserPropertyValues.Where(x => x.UserId == user.Id);
            foreach (var propertyValue in propertyDataToDelete)
            {
                dbContext.UserPropertyValues.Remove(propertyValue);
            }

            user.PropertyData.Clear();
            await loggedInUser.AddAudit(user, user.Name, AuditExtensions.AuditAction.Delete, mediator, cancellationToken);
            dbContext.Users.Remove(user);
            await dbContext.SaveChangesAsync(cancellationToken);
            handlerResult.Messages.Add(new ResultMessage("User deleted successfully", ResultMessageType.Success));
            handlerResult.Success = true;
        }
        else
        {
            handlerResult.AddMessage("User not found", ResultMessageType.Error);
        }

        return handlerResult;
    }
}