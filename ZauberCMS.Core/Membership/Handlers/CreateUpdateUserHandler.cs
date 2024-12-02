using AutoMapper;
using MediatR;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Email.Commands;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Membership.Commands;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Plugins;
using ZauberCMS.Core.Providers;
using ZauberCMS.Core.Shared.Models;
using ZauberCMS.Core.Shared.Services;

namespace ZauberCMS.Core.Membership.Handlers;

public class CreateUpdateUserHandler(
    ILogger<CreateUpdateUserHandler> logger,
    AuthenticationStateProvider authenticationStateProvider,
    IServiceProvider serviceProvider,
    IMapper mapper,
    ICacheService cacheService,
    ExtensionManager extensionManager)
    : IRequestHandler<CreateUpdateUserCommand, HandlerResult<User>>
{
    
    public async Task<HandlerResult<User>> Handle(CreateUpdateUserCommand request, CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var dbContext = scope.ServiceProvider.GetRequiredService<ZauberDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

        // Get the current user first via the authstate
        var authState = await authenticationStateProvider.GetAuthenticationStateAsync();
        var handlerResult = new HandlerResult<User>();

        var user = await dbContext.Users
            //.Include(x => x.ProfileImage)
            .FirstOrDefaultAsync(x => x.Id == authState.User.GetUserId(), cancellationToken: cancellationToken);
        if (user == null)
        {
            // new users should only be created by the register page
            handlerResult.Success = false;
            handlerResult.AddMessage("Unable to create a new user, use the registration form", ResultMessageType.Error);
            return handlerResult;
        }

        //var oldProfileImageId = user.ProfileImageId;
        
        mapper.Map(request.User, user);
        
        /*
        if (request.ProfileImageUpload != null)
        {
            var socialImageFile =
                await request.ProfileImageUpload.AddFileToDb(request.User.Id, handlerResult, _providerService, dbContext);
            user.ProfileImage = socialImageFile;
            user.ProfileImageId = socialImageFile?.Id;

            // If an old image existed, delete it
            if (oldProfileImageId != null)
            {
                var oldSocialImage = await dbContext.Files.FindAsync(new object?[] {oldProfileImageId},
                    cancellationToken: cancellationToken);
                if (oldSocialImage != null)
                {
                    dbContext.Files.Remove(oldSocialImage);
                }
            }
        }
        */
        
        handlerResult = await dbContext.SaveChangesAndLog(user, handlerResult, cacheService, extensionManager, cancellationToken);
        if (!handlerResult.Success)
        {
            return handlerResult;
        }

        // Get user from user manager to update all this
        var managerUser = await userManager.GetUserAsync(authState.User);

        if (managerUser != null)
        {
            // Need to use user manager and then refresh signin.
            if (request.User.UserName != managerUser.UserName)
            {
                var userNameResult = await userManager.SetUserNameAsync(managerUser, request.User.UserName);
                if (userNameResult.Succeeded)
                {
                    handlerResult.RefreshSignIn = true;
                    handlerResult.AddMessage("Username updated successfully", ResultMessageType.Success);
                }
                else
                {
                    handlerResult.Success = userNameResult.Succeeded;
                    handlerResult.AddMessage(userNameResult.ToErrorsList(), ResultMessageType.Error);
                    userNameResult.LogErrors(logger);
                    return handlerResult;
                }
            }

            // Need to use user manager and then refresh signin. So save other fields first
            if (request.User.Email != managerUser.Email)
            {
                // See if email needs to be confirmed
                if (userManager.Options.SignIn.RequireConfirmedAccount)
                {
                    var sendConfirmationEmailCommand = new SendEmailConfirmationCommand
                    {
                        User = managerUser,
                        NewEmailAddress = request.User.Email
                    };

                    // Send the email
                    await mediator.Send(sendConfirmationEmailCommand, cancellationToken);

                    // Save the new email in the users extended data
                    managerUser.ExtendedData.Add(Constants.ExtendedDataKeys.NewEmailAddress, request.User.Email!);

                    handlerResult.Success = true;
                    handlerResult.RefreshSignIn = true;
                    handlerResult.AddMessage("Please check your email and click the link to confirm your email address",
                        ResultMessageType.Info);
                }
                else
                {
                    // Just generate the code and change the email
                    var code = await userManager.GenerateChangeEmailTokenAsync(managerUser, request.User.Email!);
                    var emailResult =
                        await userManager.ChangeEmailAsync(managerUser, request.User.Email!, code);
                    if (emailResult.Succeeded)
                    {
                        handlerResult.Success = true;
                        handlerResult.RefreshSignIn = true;
                        handlerResult.AddMessage("Email address changed", ResultMessageType.Success);
                    }
                    else
                    {
                        handlerResult.Success = emailResult.Succeeded;
                        handlerResult.AddMessage(emailResult.ToErrorsList(), ResultMessageType.Error);
                        emailResult.LogErrors(logger);
                        return handlerResult;
                    }
                }
            }

            // Password. Again need to use user manager
            if (!request.CurrentPassword.IsNullOrWhiteSpace() && !request.NewPassword.IsNullOrWhiteSpace())
            {
                var changePasswordResult = await userManager
                    .ChangePasswordAsync(managerUser, request.CurrentPassword, request.NewPassword);
                if (changePasswordResult.Succeeded)
                {
                    handlerResult.Success = true;
                    handlerResult.RefreshSignIn = true;
                    handlerResult.AddMessage("Password changed", ResultMessageType.Success);
                }
                else
                {
                    handlerResult.Success = changePasswordResult.Succeeded;
                    handlerResult.AddMessage(changePasswordResult.ToErrorsList(), ResultMessageType.Error);
                    changePasswordResult.LogErrors(logger);
                    return handlerResult;
                }
            }

            // Messages to TempUiMessages if refresh sign in
            if (handlerResult.RefreshSignIn)
            {
                    managerUser.ExtendedData.RemoveTempUiMessages();
                managerUser.ExtendedData.SetTempUiMessage(handlerResult.Messages);
                var tempUiUpdateResult = await userManager.UpdateAsync(managerUser);
                if (!tempUiUpdateResult.Succeeded)
                {
                    tempUiUpdateResult.LogErrors(logger);
                }
            }
        }
        else
        {
            handlerResult.Success = false;
            handlerResult.AddMessage("Unable to get the database user from the logged in user", ResultMessageType.Error);
        }

        // Clear user caches
        cacheService.ClearCachedItemsWithPrefix(nameof(User));

        return handlerResult;
    }
}