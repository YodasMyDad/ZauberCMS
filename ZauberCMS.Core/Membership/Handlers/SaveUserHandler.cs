using AutoMapper;
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

public class SaveUserHandler(
    IServiceProvider serviceProvider,
    IMediator mediator,
    IMapper mapper,
    AuthenticationStateProvider authenticationStateProvider)
    : IRequestHandler<SaveUserCommand, HandlerResult<User>>
{
    public async Task<HandlerResult<User>> Handle(SaveUserCommand request, CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var authState = await authenticationStateProvider.GetAuthenticationStateAsync();
        var loggedInUser = await userManager.GetUserAsync(authState.User);
        var dbContext = scope.ServiceProvider.GetRequiredService<ZauberDbContext>();
        var refreshCurrentUser = false;
        var isUpdate = false;
        var handlerResult = new HandlerResult<User>();
        if (request.User != null)
        {
            var user = await userManager.FindByIdAsync(request.User.Id.ToString());

            if (user == null)
            {
                user = request.User;
                var result = await userManager.CreateAsync(user, request.Password!);
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
                isUpdate = true;
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
                
                await loggedInUser.AddAudit(request.User, request.User.Name, isUpdate ? AuditExtensions.AuditAction.Update : AuditExtensions.AuditAction.Create, mediator, cancellationToken);
                
                // Finally update property data
                handlerResult = await UpdateUserPropertyValues(dbContext, request.User, handlerResult, cancellationToken);
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
    
    private async Task<HandlerResult<User>> UpdateUserPropertyValues(ZauberDbContext dbContext, User requestUser, HandlerResult<User> handlerResult, CancellationToken cancellationToken)
    {

        var user = dbContext.Users.Include(x => x.PropertyData).FirstOrDefault(x => x.Id == requestUser.Id);

        // Remove deleted items
        var deletedItems = user!.PropertyData.Where(epv => requestUser.PropertyData.All(npv => npv.Id != epv.Id)).ToList();
        foreach (var deletedItem in deletedItems)
        {
            dbContext.UserPropertyValues.Remove(deletedItem);
        }

        // Add or update items
        foreach (var newPropertyValue in requestUser.PropertyData)
        {
            var existingPropertyValue = user!.PropertyData.FirstOrDefault(epv => epv.Id == newPropertyValue.Id);
            if (existingPropertyValue == null)
            {
                // New property value
                dbContext.UserPropertyValues.Add(newPropertyValue);
            }
            else
            {
                // Existing property value, update its properties
                mapper.Map(newPropertyValue, existingPropertyValue);
            }
        }
        
        return await dbContext.SaveChangesAndLog(null, handlerResult, cancellationToken);
    }
}
