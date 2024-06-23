using AutoMapper;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Membership.Commands;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Membership.Handlers;

public class SaveUserHandler(
    IServiceProvider serviceProvider,
    IMapper mapper)
    : IRequestHandler<SaveUserCommand, HandlerResult<User>>
{
    
    public async Task<HandlerResult<User>> Handle(SaveUserCommand request, CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ZauberDbContext>();
    
        var handlerResult = new HandlerResult<User>();

        if (request.User != null)
        {
            // Get the DB version
            var user = dbContext.Users
                .Include(u => u.UserRoles) // Include UserRoles
                .FirstOrDefault(x => x.Id == request.User.Id);

            if (user == null)
            {
                user = request.User;
                dbContext.Users.Add(user);

                // Assign the passed in roles to the new user
                user.UserRoles = request.Roles.Select(roleId => new UserRole { RoleId = roleId, UserId = user.Id }).ToList();
            }
            else
            {
                // Map the updated properties
                mapper.Map(request.User, user);

                // Update the user's roles
                var currentRoleIds = user.UserRoles.Select(ur => ur.RoleId).ToList();
                var newRoleIds = request.Roles;

                // Remove roles that are no longer assigned
                var rolesToRemove = user.UserRoles.Where(ur => !newRoleIds.Contains(ur.RoleId)).ToList();
                foreach (var roleToRemove in rolesToRemove)
                {
                    user.UserRoles.Remove(roleToRemove);
                }

                // Add new roles that are assigned
                var rolesToAdd = newRoleIds.Where(roleId => !currentRoleIds.Contains(roleId)).Select(roleId => new UserRole { RoleId = roleId, UserId = user.Id }).ToList();
                user.UserRoles.AddRange(rolesToAdd);
            }

            return await dbContext.SaveChangesAndLog(user, handlerResult, cancellationToken);
        }
    
        handlerResult.AddMessage("User is null", ResultMessageType.Error);
        return handlerResult;
    }
}