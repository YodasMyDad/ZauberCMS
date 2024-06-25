using AutoMapper;
using MediatR;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Membership.Commands;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Membership.Handlers;

public class SaveUserHandler(
    IServiceProvider serviceProvider,
    IMapper mapper,
    AuthenticationStateProvider authenticationStateProvider)
    : IRequestHandler<SaveUserCommand, HandlerResult<User>>
{
    public async Task<HandlerResult<User>> Handle(SaveUserCommand request, CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var authState = await authenticationStateProvider.GetAuthenticationStateAsync();
        var refreshCurrentUser = false;
        var handlerResult = new HandlerResult<User>();
        if (request.User != null)
        {
            var user = await userManager.FindByIdAsync(request.User.Id.ToString());

            if (user == null)
            {
                user = request.User;
                var result = await userManager.CreateAsync(user, request.Password);
                if (!result.Succeeded)
                {
                    handlerResult.Messages.AddRange(result.Errors.Select(e => new ResultMessage(e.Description, ResultMessageType.Error)));
                    return handlerResult;
                }

                // set the default starting role if no roles are set
                request.Roles ??= [Constants.Roles.StandardRoleName];
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
                    if (authState.User.Identity?.IsAuthenticated == true
                        && authState.User.GetUserId() == user.Id)
                    {
                        refreshCurrentUser = true;   
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
                    if (authState.User.Identity?.IsAuthenticated == true
                        && authState.User.GetUserId() == user.Id)
                    {
                        refreshCurrentUser = true;   
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

                    if (authState.User.Identity?.IsAuthenticated == true
                        && authState.User.GetUserId() == user.Id)
                    {
                        refreshCurrentUser = true;   
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
                    
                    if (authState.User.Identity?.IsAuthenticated == true
                        && authState.User.GetUserId() == user.Id)
                    {
                        refreshCurrentUser = true;   
                    }
                }
            }

            // Update security stamp if needed
            if (refreshCurrentUser == false && userManager.SupportsUserSecurityStamp)
            {
                await userManager.UpdateSecurityStampAsync(user);
            }

            handlerResult.Entity = user;
            handlerResult.Success = true;
            handlerResult.RefreshSignIn = refreshCurrentUser;
        }
        else
        {
            handlerResult.Messages.Add(new ResultMessage("User is null", ResultMessageType.Error));
        }

        return handlerResult;
    }
}
