using AutoMapper;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
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
        //var dbContext = scope.ServiceProvider.GetRequiredService<ZauberDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        
        var handlerResult = new HandlerResult<User>();
        if (request.User != null)
        {
            var user = await userManager.FindByIdAsync(request.User.Id.ToString());

            if (user == null)
            {
                user = request.User;
                var result = await userManager.CreateAsync(user, request.User.PasswordHash); // TODO Assume PasswordHash is the plain password for simplicity
                if (!result.Succeeded)
                {
                    handlerResult.Messages.AddRange(result.Errors.Select(e => new ResultMessage(e.Description, ResultMessageType.Error)));
                    return handlerResult;
                }
            }
            else
            {
                if (user.UserName != request.User.UserName)
                {
                    var result = await userManager.SetUserNameAsync(user, request.User.UserName);
                    if (!result.Succeeded)
                    {
                        handlerResult.Messages.AddRange(result.Errors.Select(e => new ResultMessage(e.Description, ResultMessageType.Error)));
                        return handlerResult;
                    }
                }

                if (user.Email != request.User.Email)
                {
                    var result = await userManager.SetEmailAsync(user, request.User.Email);
                    if (!result.Succeeded)
                    {
                        handlerResult.Messages.AddRange(result.Errors.Select(e => new ResultMessage(e.Description, ResultMessageType.Error)));
                        return handlerResult;
                    }
                }

                // Update other properties
                mapper.Map(request.User, user);
                user.DateUpdated = DateTime.UtcNow;

                var updateResult = await userManager.UpdateAsync(user);
                if (!updateResult.Succeeded)
                {
                    handlerResult.Messages.AddRange(updateResult.Errors.Select(e => new ResultMessage(e.Description, ResultMessageType.Error)));
                    return handlerResult;
                }
            }

            // Handle roles
            if (request.Roles != null)
            {
                var currentRoles = await userManager.GetRolesAsync(user);
                var rolesToAdd = request.Roles.Except(currentRoles).ToList();
                var rolesToRemove = currentRoles.Except(request.Roles).ToList();

                if (rolesToAdd.Count != 0)
                {
                    var result = await userManager.AddToRolesAsync(user, rolesToAdd);
                    if (!result.Succeeded)
                    {
                        handlerResult.Messages.AddRange(result.Errors.Select(e => new ResultMessage(e.Description, ResultMessageType.Error)));
                        return handlerResult;
                    }
                }

                if (rolesToRemove.Count != 0)
                {
                    var result = await userManager.RemoveFromRolesAsync(user, rolesToRemove);
                    if (!result.Succeeded)
                    {
                        handlerResult.Messages.AddRange(result.Errors.Select(e => new ResultMessage(e.Description, ResultMessageType.Error)));
                        return handlerResult;
                    }
                }
            }

            // Update security stamp if needed
            if (userManager.SupportsUserSecurityStamp)
            {
                await userManager.UpdateSecurityStampAsync(user);
                handlerResult.RefreshSignIn = true;
            }

            handlerResult.Entity = user;
            handlerResult.Success = true;
        }
        else
        {
            handlerResult.Messages.Add(new ResultMessage("User is null", ResultMessageType.Error));
        }

        return handlerResult;
    }
}
