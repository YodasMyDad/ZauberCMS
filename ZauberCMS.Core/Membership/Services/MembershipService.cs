using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using AutoMapper;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Email.Commands;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Membership.Commands;
using ZauberCMS.Core.Membership.Interfaces;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Membership.Parameters;
using ZauberCMS.Core.Plugins;
using ZauberCMS.Core.Providers;
using ZauberCMS.Core.Settings;
using ZauberCMS.Core.Shared.Models;
using ZauberCMS.Core.Shared.Services;

namespace ZauberCMS.Core.Membership.Services;

public class MembershipService(
    IServiceProvider serviceProvider,
    ICacheService cacheService,
    IMapper mapper,
    AuthenticationStateProvider authenticationStateProvider,
    ExtensionManager extensionManager,
    IHttpContextAccessor httpContextAccessor,
    ProviderService providerService,
    IOptions<ZauberSettings> settings,
    ILogger<MembershipService> logger) : IMembershipService
{
    public async Task<User?> GetUserAsync(GetUserParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var cacheKey = GenerateCacheKey(parameters, dbContext);

        if (parameters.Cached)
        {
            return await cacheService.GetSetCachedItemAsync(cacheKey, async () => await FetchUserAsync(parameters, dbContext, cancellationToken));
        }

        return await FetchUserAsync(parameters, dbContext, cancellationToken);
    }

    public async Task<HandlerResult<User>> SaveUserAsync(SaveUserParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var authState = await authenticationStateProvider.GetAuthenticationStateAsync();
        var loggedInUser = await userManager.GetUserAsync(authState.User);
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var refreshCurrentUser = false;
        var isUpdate = false;
        var handlerResult = new HandlerResult<User>();
        
        if (parameters.User != null)
        {
            var user = await userManager.FindByIdAsync(parameters.User.Id.ToString());

            if (user == null)
            {
                user = parameters.User;
                var result = await userManager.CreateAsync(user, parameters.Password!);
                if (!result.Succeeded)
                {
                    handlerResult.Messages.AddRange(result.Errors.Select(e => new ResultMessage(e.Description, ResultMessageType.Error)));
                    return handlerResult;
                }

                // set the default starting role if no roles are set
                parameters.Roles ??= [Constants.Roles.StandardRoleName];
            }
            else
            {
                isUpdate = true;
                if (user.UserName != parameters.User.UserName)
                {
                    var result = await userManager.SetUserNameAsync(user, parameters.User.UserName);
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

                if (user.Email != parameters.User.Email)
                {
                    var result = await userManager.SetEmailAsync(user, parameters.User.Email);
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
                mapper.Map(parameters.User, user);
                user.DateUpdated = DateTime.UtcNow;
                
                var updateResult = await userManager.UpdateAsync(user);
                if (!updateResult.Succeeded)
                {
                    handlerResult.Messages.AddRange(updateResult.Errors.Select(e => new ResultMessage(e.Description, ResultMessageType.Error)));
                    return handlerResult;
                }
                
                await loggedInUser.AddAudit(parameters.User, parameters.User.Name, isUpdate ? AuditExtensions.AuditAction.Update : AuditExtensions.AuditAction.Create, null, cancellationToken);
                
                // Finally update property data
                handlerResult = await UpdateUserPropertyValues(dbContext, parameters.User, handlerResult, cancellationToken);
            }

            // Handle roles
            if (parameters.Roles != null)
            {
                var currentRoles = await userManager.GetRolesAsync(user);
                var rolesToAdd = parameters.Roles.Except(currentRoles).ToList();
                var rolesToRemove = currentRoles.Except(parameters.Roles).ToList();

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

    public async Task<HandlerResult<User>> CreateUpdateUserAsync(CreateUpdateUserParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

        // Get the current user first via the authstate
        var authState = await authenticationStateProvider.GetAuthenticationStateAsync();
        var handlerResult = new HandlerResult<User>();

        var user = await dbContext.Users
            .FirstOrDefaultAsync(x => x.Id == authState.User.GetUserId(), cancellationToken: cancellationToken);
        if (user == null)
        {
            // new users should only be created by the register page
            handlerResult.Success = false;
            handlerResult.AddMessage("Unable to create a new user, use the registration form", ResultMessageType.Error);
            return handlerResult;
        }

        // Map the updated properties
        mapper.Map(parameters.User, user);
        user.DateUpdated = DateTime.UtcNow;

        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            handlerResult.Messages.AddRange(updateResult.Errors.Select(e => new ResultMessage(e.Description, ResultMessageType.Error)));
            return handlerResult;
        }

        // Handle roles if provided
        if (parameters.Roles != null)
        {
            var currentRoles = await userManager.GetRolesAsync(user);
            var rolesToAdd = parameters.Roles.Except(currentRoles).ToList();
            var rolesToRemove = currentRoles.Except(parameters.Roles).ToList();

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

        // Update property data
        handlerResult = await UpdateUserPropertyValues(dbContext, parameters.User, handlerResult, cancellationToken);

        handlerResult.Entity = user;
        handlerResult.Success = true;
        handlerResult.RefreshSignIn = true;

        return handlerResult;
    }

    public async Task<HandlerResult<User>> DeleteUserAsync(DeleteUserParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var authState = await authenticationStateProvider.GetAuthenticationStateAsync();
        var loggedInUser = await userManager.GetUserAsync(authState.User);
        var handlerResult = new HandlerResult<User>();

        var user = await dbContext.Users
            .Include(u => u.UserRoles) // Include UserRoles to delete related roles
            .FirstOrDefaultAsync(x => x.Id == parameters.Id, cancellationToken);

        if (user != null)
        {
            // Now delete the PropertyData
            var propertyDataToDelete = dbContext.UserPropertyValues.Where(x => x.UserId == user.Id);
            foreach (var propertyValue in propertyDataToDelete)
            {
                dbContext.UserPropertyValues.Remove(propertyValue);
            }

            user.PropertyData.Clear();
            await loggedInUser.AddAudit(user, user.Name, AuditExtensions.AuditAction.Delete, null, cancellationToken);
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

    public async Task<PaginatedList<User>> QueryUsersAsync(QueryUsersParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var query = BuildUsersQuery(parameters, dbContext);
        var cacheKey = query.GenerateCacheKey(typeof(User));

        if (parameters.Cached)
        {
            return (await cacheService.GetSetCachedItemAsync(cacheKey, async () => await FetchUsersAsync(parameters, dbContext, cancellationToken)))!;
        }

        return await FetchUsersAsync(parameters, dbContext, cancellationToken);
    }

    public async Task<Role?> GetRoleAsync(GetRoleParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var query = dbContext.Roles.AsQueryable();

        if (parameters.AsNoTracking)
        {
            query = query.AsNoTracking();
        }

        if (parameters.Id != null)
        {
            return await query.FirstOrDefaultAsync(x => x.Id == parameters.Id, cancellationToken: cancellationToken);
        }

        if (!parameters.Name.IsNullOrWhiteSpace())
        {
            return await query.FirstOrDefaultAsync(x => x.Name == parameters.Name, cancellationToken: cancellationToken);
        }

        return await query.FirstOrDefaultAsync(cancellationToken: cancellationToken);
    }

    public async Task<HandlerResult<Role>> SaveRoleAsync(SaveRoleParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var authState = await authenticationStateProvider.GetAuthenticationStateAsync();
        var loggedInUser = await userManager.GetUserAsync(authState.User);
        var handlerResult = new HandlerResult<Role>();
        var isUpdate = false;
        
        if (parameters.Role != null)
        {
            // Get the DB version
            var role = dbContext.Roles
                .FirstOrDefault(x => x.Id == parameters.Role.Id);

            if (role == null)
            {
                role = parameters.Role;
                dbContext.Roles.Add(role);
            }
            else
            {
                isUpdate = true;
                // Map the updated properties
                mapper.Map(parameters.Role, role);
                role.DateUpdated = DateTime.UtcNow;
            }
            
            await loggedInUser.AddAudit(role, role.Name, isUpdate ? AuditExtensions.AuditAction.Update : AuditExtensions.AuditAction.Create, null, cancellationToken);
            return await dbContext.SaveChangesAndLog(role, handlerResult, cacheService, extensionManager, cancellationToken);
        }

        handlerResult.AddMessage("Role is null", ResultMessageType.Error);
        return handlerResult;
    }

    public async Task<HandlerResult<Role>> DeleteRoleAsync(DeleteRoleParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var authState = await authenticationStateProvider.GetAuthenticationStateAsync();
        var loggedInUser = await userManager.GetUserAsync(authState.User);
        var handlerResult = new HandlerResult<Role>();

        var role = await dbContext.Roles
            .Include(r => r.UserRoles) // Include UserRoles to delete related user-role relationships
            .FirstOrDefaultAsync(x => x.Id == parameters.Id, cancellationToken);

        if (role != null)
        {
            var usersInThisRole = await QueryUsersAsync(new QueryUsersParameters { Roles = [role.Name!] }, cancellationToken);
            if (usersInThisRole.Items.Any())
            {
                // Display error message
                handlerResult.Messages.Add(new ResultMessage("Unable to delete as users are in this role",
                    ResultMessageType.Error));
                return handlerResult;
            }

            await loggedInUser.AddAudit(role, role.Name, AuditExtensions.AuditAction.Delete, null, cancellationToken);
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

    public async Task<PaginatedList<Role>> QueryRolesAsync(QueryRolesParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var query = dbContext.Roles.AsQueryable();

        if (parameters.Query != null)
        {
            query = parameters.Query.Invoke();
        }
        else
        {
            if (parameters.AsNoTracking)
            {
                query = query.AsNoTracking();
            }

            var idCount = parameters.Ids.Count;
            if (parameters.Ids.Count != 0)
            {
                query = query.Where(x => parameters.Ids.Contains(x.Id));
                parameters.AmountPerPage = idCount;
            }
        }
        
        if (parameters.WhereClause != null)
        {
            query = query.Where(parameters.WhereClause);
        }   

        query = parameters.OrderBy switch
        {
            GetRolesOrderBy.DateCreated => query.OrderBy(p => p.DateCreated),
            GetRolesOrderBy.DateCreatedDescending => query.OrderByDescending(p => p.DateCreated),
            GetRolesOrderBy.DateUpdated => query.OrderBy(p => p.DateUpdated),
            GetRolesOrderBy.DateUpdatedDescending => query.OrderByDescending(p => p.DateUpdated),
            GetRolesOrderBy.Name => query.OrderBy(p => p.Name),
            _ => query.OrderByDescending(p => p.DateCreated)
        };
        
        return query.ToPaginatedList(parameters.PageIndex, parameters.AmountPerPage);
    }

    public async Task<AuthenticationResult> LoginUserAsync(LoginUserParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var signInManager = scope.ServiceProvider.GetRequiredService<SignInManager<User>>();
        
        // This doesn't count login failures towards account lockout
        // To enable password failures to trigger account lockout, set lockoutOnFailure: true
        var loginResult = new AuthenticationResult();
        
        try
        {
            await signInManager.SignOutAsync();
            var user = await userManager.FindByEmailAsync(parameters.Email);
            if (user != null)
            {
                var signInResult = await signInManager.PasswordSignInAsync(user, parameters.Password, parameters.RememberMe, false);
                loginResult.Success = signInResult.Succeeded;

                if (loginResult.Success)
                {
                    var userPrincipal = await signInManager.CreateUserPrincipalAsync(user);
                    if (parameters.ReturnUrl.IsNullOrWhiteSpace() && userPrincipal.IsInRole(Constants.Roles.AdminRoleName))
                    {
                        parameters.ReturnUrl = Urls.AdminBaseUrl;
                    }
                    loginResult.NavigateToUrl = parameters.ReturnUrl;
                }
                else
                {
                    if (signInResult.IsNotAllowed)
                    {
                        if (!await userManager.IsEmailConfirmedAsync(user))
                        {
                            loginResult.AddMessage("Email isn't confirmed. Check your inbox for a confirmation email", ResultMessageType.Warning);

                            // Resend confirmation email - would need to implement email service call here
                            // For now, just add the message
                        }
                    }
                    else if (signInResult.IsLockedOut)
                    {
                        logger.LogWarning("User {RequestEmail} account is locked out", parameters.Email);
                        loginResult.AddMessage("Account is locked out.", ResultMessageType.Error);
                    }
                    else if (signInResult.RequiresTwoFactor)
                    {
                        // This is currently not supported
                        loginResult.NavigateToUrl = $"{Urls.Account.LoginWith2Fa}?returnUrl={parameters.ReturnUrl}&rememberMe={parameters.RememberMe}";
                    }
                    else
                    {
                        loginResult.AddMessage("Password is incorrect", ResultMessageType.Error);
                    }
                }
            }
            else
            {
                loginResult.AddMessage("You are do not have an account, please register", ResultMessageType.Error);
            }
        }
        catch (Exception e)
        {
           loginResult.AddMessage(e.Message, ResultMessageType.Error);
           loginResult.Success = false;
           return loginResult;
        }

        return loginResult;
    }

    public async Task<AuthenticationResult> RegisterUserAsync(RegisterUserParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var signInManager = scope.ServiceProvider.GetRequiredService<SignInManager<User>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<Role>>();
            
        var newUser = new User 
        { 
            Id = Guid.NewGuid().NewSequentialGuid(), 
            Email = parameters.Email, 
            UserName = parameters.Email,
            FirstName = parameters.FirstName,
            LastName = parameters.LastName,
            PhoneNumber = parameters.PhoneNumber,
            DateOfBirth = parameters.DateOfBirth,
            Bio = parameters.Bio,
            Headline = parameters.Headline
        };
        
        var loginResult = new AuthenticationResult();
        var createResult = await userManager.CreateAsync(newUser, parameters.Password);

        loginResult.Success = createResult.Succeeded;
        if (loginResult.Success)
        {
            loginResult = await userManager.AssignStartingRoleAsync(
                                            roleManager,
                                            logger,
                                            dbContext,
                                            settings,
                                            null,
                                            newUser,
                                            loginResult);

            if (!loginResult.Success)
            {
                return loginResult;
            }

            // Handle email confirmation
            if (parameters.SendConfirmationEmail && !parameters.AutoConfirmEmail)
            {
                // Would need to implement email confirmation sending here
                // For now just set the result
                loginResult.AddMessage("Please check your email to confirm your account", ResultMessageType.Success);
            }
            else if (parameters.AutoConfirmEmail)
            {
                var token = await userManager.GenerateEmailConfirmationTokenAsync(newUser);
                await userManager.ConfirmEmailAsync(newUser, token);
            }

            // Auto sign in if requested
            if (parameters.AutoLogin && loginResult.Success)
            {
                await signInManager.SignInAsync(newUser, isPersistent: false);
                loginResult.NavigateToUrl = Urls.AdminBaseUrl;
            }
        }
        else
        {
            loginResult.AddMessage(createResult.Errors.Select(e => e.Description).ToList(), ResultMessageType.Error);
        }

        return loginResult;
    }

    public async Task<AuthenticationResult> ExternalLoginAsync(ExternalLoginParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var signInManager = scope.ServiceProvider.GetRequiredService<SignInManager<User>>();
        var userStore = scope.ServiceProvider.GetRequiredService<IUserStore<User>>();
        var emailStore = scope.ServiceProvider.GetRequiredService<IUserEmailStore<User>>();

        var authenticationResult = new AuthenticationResult();

        // This would need to be implemented based on the actual external login info
        // For now, just return a basic implementation
        authenticationResult.Success = false;
        authenticationResult.AddMessage("External login not fully implemented", ResultMessageType.Error);
        
        return authenticationResult;
    }

    public async Task<AuthenticationResult> ConfirmEmailAsync(ConfirmEmailParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var signInManager = scope.ServiceProvider.GetRequiredService<SignInManager<User>>();

        var result = new AuthenticationResult {Success = true};
        if (parameters.UserId.IsNullOrWhiteSpace())
        {
            result.Success = false;
            result.AddMessage("The user id is null", ResultMessageType.Error);
            return result;
        }

        var user = await userManager.FindByIdAsync(parameters.UserId);
        if (user == null)
        {
            result.Success = false;
            result.AddMessage($"Unable to find a user with the id '{parameters.UserId}'.", ResultMessageType.Error);
            return result;
        }

        parameters.Code = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(parameters.Code!));

        if (parameters.ChangedEmail.IsNotNullOrWhiteSpace())
        {
            // Get the new email from the extended data
            var newEmail = user!.ExtendedData.Get(Constants.ExtendedDataKeys.NewEmailAddress);
            if (!newEmail.IsNullOrWhiteSpace())
            {
                var changeResult = await userManager.ChangeEmailAsync(user, newEmail, parameters.Code);
                if (!changeResult.Succeeded)
                {
                    result.Success = false;
                    changeResult.LogErrors();
                    result.AddMessage(changeResult.ToErrorsList(), ResultMessageType.Error);
                    return result;
                }

                // Clear new email from user
                user.ExtendedData.Remove(Constants.ExtendedDataKeys.NewEmailAddress);
                var updateResult = await userManager.UpdateAsync(user);
                if (!updateResult.Succeeded)
                {
                    updateResult.LogErrors();
                    return result;
                }

                await signInManager.RefreshSignInAsync(user);

                // return success message
                result.AddMessage("Email address changed", ResultMessageType.Success);
            }
            else
            {
                // error unable to get new email address
                result.Success = false;
                result.AddMessage("Unable to get users new email address", ResultMessageType.Error);
                return result;
            }
        }
        else
        {
            var confirmResult = await userManager.ConfirmEmailAsync(user!, parameters.Code);
            if (confirmResult.Succeeded)
            {
                result.AddMessage("Email confirmed, you can now login", ResultMessageType.Success);
            }
            else
            {
                result.Success = false;
                result.AddMessage("There was an error confirming your email", ResultMessageType.Error);
            }
        }

        return result;
    }

    public async Task<AuthenticationResult> ForgotPasswordAsync(ForgotPasswordParameters parameters, CancellationToken cancellationToken = default)
    {
        var result = new AuthenticationResult();

        using var scope = serviceProvider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

        if (parameters.Email != null)
        {
            var user = await userManager.FindByEmailAsync(parameters.Email);
            if (user != null)
            {
                if (userManager.Options.SignIn.RequireConfirmedAccount && await userManager.IsEmailConfirmedAsync(user) == false)
                {
                    result.Success = false;
                    result.AddMessage("Please check your email to confirm your account", ResultMessageType.Success);

                    // Would need to implement resend confirmation email here
                    return result;
                }

                // For more information on how to enable account confirmation and password reset please
                // visit https://go.microsoft.com/fwlink/?LinkID=532713
                var code = await userManager.GeneratePasswordResetTokenAsync(user);
                code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
                var callbackUrl = httpContextAccessor.ToAbsoluteUrl(Urls.Account.ResetPassword, new { code = code, email = parameters.Email });

                var paragraphs = new List<string> { $"Please reset your password by <a class=\"underline\" href='{HtmlEncoder.Default.Encode(callbackUrl)}'>clicking here</a>." };
                await providerService.EmailProvider!.SendEmailWithTemplateAsync(parameters.Email, "Reset Password", paragraphs);
            }
        }

        result.Success = true;
        result.AddMessage("An email has been sent to you to", ResultMessageType.Success);

        return result;
    }

    public async Task<AuthenticationResult> ResetPasswordAsync(ResetPasswordParameters parameters, CancellationToken cancellationToken = default)
    {
        var result = new AuthenticationResult();
        using var scope = serviceProvider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var user = await userManager.FindByEmailAsync(parameters.Email!);
        if (user != null)
        {
            var resetResult = await userManager.ResetPasswordAsync(user, parameters.ResetCode!, parameters.NewPassword!);
            if (resetResult.Succeeded == false)
            {
                result.Success = false;
                foreach (var error in resetResult.Errors)
                {
                    result.AddMessage(error.Description, ResultMessageType.Error);
                }
                return result;
            }
        }

        result.Success = true;
        result.AddMessage($"Your password has been reset, <a class=\"underline\" href=\"{Urls.Account.Login}\">please login</a>", ResultMessageType.Success);
        return result;
    }

    public async Task<User?> GetCurrentUserAsync(GetCurrentUserParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var authState = await authenticationStateProvider.GetAuthenticationStateAsync();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        
        var user = await userManager.GetUserAsync(authState.User);
        if (user != null && parameters.IncludeRoles)
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
            user = await dbContext.Users
                .Include(x => x.UserRoles)
                .ThenInclude(x => x.Role)
                .FirstOrDefaultAsync(x => x.Id == user.Id, cancellationToken);
        }
        
        return user;
    }

    // Private helper methods
    private static string GenerateCacheKey(GetUserParameters parameters, IZauberDbContext dbContext)
    {
        var query = BuildUserQuery(parameters, dbContext);
        var queryString = query.ToQueryString();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(queryString));
        return typeof(User).ToCacheKey(Convert.ToBase64String(hash));
    }

    private static IQueryable<User> BuildUserQuery(GetUserParameters parameters, IZauberDbContext dbContext)
    {
        var query = dbContext.Users
            .Include(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .Include(x => x.PropertyData)
            .AsNoTracking()
            .AsSplitQuery()
            .AsQueryable();

        if (parameters.Id.HasValue)
        {
            return query.Where(x => x.Id == parameters.Id);
        }
        
        if (!parameters.Email.IsNullOrWhiteSpace())
        {
            return query.Where(x => x.Email == parameters.Email);
        }

        return query;
    }

    private static async Task<User?> FetchUserAsync(GetUserParameters parameters, IZauberDbContext dbContext, CancellationToken cancellationToken)
    {
        var query = BuildUserQuery(parameters, dbContext);
        return await query.FirstOrDefaultAsync(cancellationToken: cancellationToken);
    }

    private static IQueryable<User> BuildUsersQuery(QueryUsersParameters parameters, IZauberDbContext dbContext)
    {
        var query = dbContext.Users.Include(x => x.UserRoles).AsQueryable();

        if (parameters.Query != null)
        {
            query = parameters.Query.Invoke();
        }
        else
        {
            if (parameters.AsNoTracking)
            {
                query = query.AsNoTracking();
            }

            if (!string.IsNullOrWhiteSpace(parameters.SearchTerm))
            {
                var searchTermLower = parameters.SearchTerm.ToLower();
                query = query.Where(x => x.UserName != null && x.UserName.ToLower().Contains(searchTermLower));
            }

            if (parameters.Roles.Count != 0)
            {
                query = query.Where(x => x.UserRoles.Any(ur => ur.Role.Name != null && parameters.Roles.Contains(ur.Role.Name)));
            }

            if (parameters.Ids.Count != 0)
            {
                query = query.Where(x => parameters.Ids.Contains(x.Id));
                parameters.AmountPerPage = parameters.Ids.Count;
            }
        }

        if (parameters.WhereClause != null)
        {
            query = query.Where(parameters.WhereClause);
        }

        query = parameters.OrderBy switch
        {
            GetUsersOrderBy.DateUpdated => query.OrderBy(p => p.DateCreated),
            GetUsersOrderBy.DateCreatedDescending => query.OrderByDescending(p => p.DateCreated),
            _ => query.OrderByDescending(p => p.DateCreated)
        };

        return query;
    }

    private static Task<PaginatedList<User>> FetchUsersAsync(QueryUsersParameters parameters, IZauberDbContext dbContext, CancellationToken cancellationToken)
    {
        var query = BuildUsersQuery(parameters, dbContext);
        return Task.FromResult(query.ToPaginatedList(parameters.PageIndex, parameters.AmountPerPage));
    }
    
    private async Task<HandlerResult<User>> UpdateUserPropertyValues(IZauberDbContext dbContext, User requestUser, HandlerResult<User> handlerResult, CancellationToken cancellationToken)
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
        
        return await dbContext.SaveChangesAndLog(user, handlerResult, cacheService, extensionManager, cancellationToken);
    }
}