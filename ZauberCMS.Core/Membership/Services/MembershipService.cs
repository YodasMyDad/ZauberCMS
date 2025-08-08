using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AutoMapper;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Email.Commands;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Membership.Interfaces;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Membership.Parameters;
using ZauberCMS.Core.Plugins;
using ZauberCMS.Core.Shared.Models;
using ZauberCMS.Core.Shared.Services;

namespace ZauberCMS.Core.Membership.Services;

public class MembershipService(
    IServiceProvider serviceProvider,
    IMapper mapper,
    ICacheService cacheService,
    AuthenticationStateProvider authenticationStateProvider,
    ExtensionManager extensionManager,
    ILogger<MembershipService> logger)
    : IMembershipService
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
                
                // Note: Audit logging would need to be implemented without mediator
                logger.LogInformation("Audit logging for user {UserName} {Action}", parameters.User.Name, isUpdate ? "Update" : "Create");
                
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
                
                // Note: Audit logging would need to be implemented without mediator
                logger.LogInformation("Audit logging for user {UserName} {Action}", parameters.User.Name, isUpdate ? "Update" : "Create");
                
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

    public async Task<HandlerResult<User>> DeleteUserAsync(DeleteUserParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var authState = await authenticationStateProvider.GetAuthenticationStateAsync();
        var loggedInUser = await userManager.GetUserAsync(authState.User);
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var handlerResult = new HandlerResult<User>();

        var user = await userManager.FindByIdAsync(parameters.Id.ToString());
        if (user != null)
        {
            // Note: Audit logging would need to be implemented without mediator
            logger.LogInformation("Audit logging for user {UserName} Delete", user.Name);
            
            var result = await userManager.DeleteAsync(user);
            if (!result.Succeeded)
            {
                handlerResult.Messages.AddRange(result.Errors.Select(e => new ResultMessage(e.Description, ResultMessageType.Error)));
                return handlerResult;
            }

            handlerResult.Entity = user;
            handlerResult.Success = true;
        }
        else
        {
            handlerResult.Messages.Add(new ResultMessage("User not found", ResultMessageType.Error));
        }

        return handlerResult;
    }

    public async Task<PaginatedList<User>> QueryUsersAsync(QueryUsersParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var query = BuildQuery(parameters, dbContext);
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
        
        var query = dbContext.Roles
            .Include(x => x.UserRoles)
            .ThenInclude(x => x.User)
            .AsNoTracking()
            .AsSplitQuery()
            .Where(x => x.Id == parameters.Id);

        return await query.FirstOrDefaultAsync(cancellationToken: cancellationToken);
    }

    public async Task<HandlerResult<Role>> SaveRoleAsync(SaveRoleParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<Role>>();
        var authState = await authenticationStateProvider.GetAuthenticationStateAsync();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var loggedInUser = await userManager.GetUserAsync(authState.User);
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var handlerResult = new HandlerResult<Role>();

        if (parameters.Role != null)
        {
            var role = await roleManager.FindByIdAsync(parameters.Role.Id.ToString());

            if (role == null)
            {
                role = parameters.Role;
                var result = await roleManager.CreateAsync(role);
                if (!result.Succeeded)
                {
                    handlerResult.Messages.AddRange(result.Errors.Select(e => new ResultMessage(e.Description, ResultMessageType.Error)));
                    return handlerResult;
                }
            }
            else
            {
                role.Name = parameters.Role.Name;
                role.NormalizedName = parameters.Role.NormalizedName;
                role.DateUpdated = DateTime.UtcNow;
                
                var result = await roleManager.UpdateAsync(role);
                if (!result.Succeeded)
                {
                    handlerResult.Messages.AddRange(result.Errors.Select(e => new ResultMessage(e.Description, ResultMessageType.Error)));
                    return handlerResult;
                }
            }

            // Note: Audit logging would need to be implemented without mediator
            logger.LogInformation("Audit logging for role {RoleName} {Action}", role.Name, role.Id == Guid.Empty ? "Create" : "Update");

            handlerResult.Entity = role;
            handlerResult.Success = true;
        }
        else
        {
            handlerResult.Messages.Add(new ResultMessage("Role is null", ResultMessageType.Error));
        }

        return handlerResult;
    }

    public async Task<HandlerResult<Role>> DeleteRoleAsync(DeleteRoleParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<Role>>();
        var authState = await authenticationStateProvider.GetAuthenticationStateAsync();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var loggedInUser = await userManager.GetUserAsync(authState.User);
        var handlerResult = new HandlerResult<Role>();

        var role = await roleManager.FindByIdAsync(parameters.Id.ToString());
        if (role != null)
        {
            // Note: Audit logging would need to be implemented without mediator
            logger.LogInformation("Audit logging for role {RoleName} Delete", role.Name);
            
            var result = await roleManager.DeleteAsync(role);
            if (!result.Succeeded)
            {
                handlerResult.Messages.AddRange(result.Errors.Select(e => new ResultMessage(e.Description, ResultMessageType.Error)));
                return handlerResult;
            }

            handlerResult.Entity = role;
            handlerResult.Success = true;
        }
        else
        {
            handlerResult.Messages.Add(new ResultMessage("Role not found", ResultMessageType.Error));
        }

        return handlerResult;
    }

    public Task<PaginatedList<Role>> QueryRolesAsync(QueryRolesParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        
        var query = dbContext.Roles
            .Include(x => x.UserRoles)
            .ThenInclude(x => x.User)
            .AsQueryable();

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
                query = query.Where(x => x.Name != null && x.Name.ToLower().Contains(searchTermLower));
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
            GetRolesOrderBy.Name => query.OrderBy(p => p.Name),
            GetRolesOrderBy.NameDescending => query.OrderByDescending(p => p.Name),
            GetRolesOrderBy.DateCreated => query.OrderBy(p => p.DateCreated),
            GetRolesOrderBy.DateCreatedDescending => query.OrderByDescending(p => p.DateCreated),
            _ => query.OrderByDescending(p => p.DateCreated)
        };

        return Task.FromResult(query.ToPaginatedList(parameters.PageIndex, parameters.AmountPerPage));
    }

    public async Task<AuthenticationResult> LoginUserAsync(LoginUserParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var signInManager = scope.ServiceProvider.GetRequiredService<SignInManager<User>>();
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

                            // Resend confirmation email
                            var sendConfirmationEmailCommand = new SendEmailConfirmationCommand
                            {
                                ReturnUrl = parameters.ReturnUrl,
                                User = user
                            };

                            // Note: This would need to be handled differently without mediator
                            // For now, we'll just log that this needs to be implemented
                            logger.LogWarning("Email confirmation sending needs to be implemented without mediator");
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
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var signInManager = scope.ServiceProvider.GetRequiredService<SignInManager<User>>();
        var registrationResult = new AuthenticationResult();

        try
        {
            var user = new User
            {
                UserName = parameters.Email,
                Email = parameters.Email,
                Name = parameters.Name,
                DateCreated = DateTime.UtcNow,
                DateUpdated = DateTime.UtcNow
            };

            var result = await userManager.CreateAsync(user, parameters.Password);
            if (result.Succeeded)
            {
                // Add default role
                await userManager.AddToRoleAsync(user, Constants.Roles.StandardRoleName);

                // Sign in the user
                await signInManager.SignInAsync(user, isPersistent: false);

                registrationResult.Success = true;
                registrationResult.NavigateToUrl = parameters.ReturnUrl;
            }
            else
            {
                registrationResult.Messages.AddRange(result.Errors.Select(e => new ResultMessage(e.Description, ResultMessageType.Error)));
            }
        }
        catch (Exception e)
        {
            registrationResult.AddMessage(e.Message, ResultMessageType.Error);
            registrationResult.Success = false;
        }

        return registrationResult;
    }

    public async Task<AuthenticationResult> ExternalLoginAsync(ExternalLoginParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var signInManager = scope.ServiceProvider.GetRequiredService<SignInManager<User>>();
        var loginResult = new AuthenticationResult();

        try
        {
            var info = await signInManager.GetExternalLoginInfoAsync();
            if (info == null)
            {
                loginResult.AddMessage("Error loading external login information", ResultMessageType.Error);
                return loginResult;
            }

            var signInResult = await signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor: true);
            if (signInResult.Succeeded)
            {
                loginResult.Success = true;
                loginResult.NavigateToUrl = parameters.ReturnUrl;
            }
            else if (signInResult.IsLockedOut)
            {
                loginResult.AddMessage("Account is locked out", ResultMessageType.Error);
            }
            else
            {
                // User doesn't have an account, create one
                var user = new User
                {
                    UserName = info.Principal.FindFirstValue(ClaimTypes.Email),
                    Email = info.Principal.FindFirstValue(ClaimTypes.Email),
                    Name = info.Principal.FindFirstValue(ClaimTypes.Name),
                    DateCreated = DateTime.UtcNow,
                    DateUpdated = DateTime.UtcNow
                };

                var result = await userManager.CreateAsync(user);
                if (result.Succeeded)
                {
                    result = await userManager.AddLoginAsync(user, info);
                    if (result.Succeeded)
                    {
                        await signInManager.SignInAsync(user, isPersistent: false);
                        loginResult.Success = true;
                        loginResult.NavigateToUrl = parameters.ReturnUrl;
                    }
                    else
                    {
                        loginResult.Messages.AddRange(result.Errors.Select(e => new ResultMessage(e.Description, ResultMessageType.Error)));
                    }
                }
                else
                {
                    loginResult.Messages.AddRange(result.Errors.Select(e => new ResultMessage(e.Description, ResultMessageType.Error)));
                }
            }
        }
        catch (Exception e)
        {
            loginResult.AddMessage(e.Message, ResultMessageType.Error);
            loginResult.Success = false;
        }

        return loginResult;
    }

    public async Task<AuthenticationResult> ConfirmEmailAsync(ConfirmEmailParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var confirmationResult = new AuthenticationResult();

        try
        {
            var user = await userManager.FindByIdAsync(parameters.UserId!);
            if (user == null)
            {
                confirmationResult.AddMessage("User not found", ResultMessageType.Error);
                return confirmationResult;
            }

            var result = await userManager.ConfirmEmailAsync(user, parameters.Code!);
            if (result.Succeeded)
            {
                confirmationResult.Success = true;
                confirmationResult.NavigateToUrl = parameters.ReturnUrl;
            }
            else
            {
                confirmationResult.Messages.AddRange(result.Errors.Select(e => new ResultMessage(e.Description, ResultMessageType.Error)));
            }
        }
        catch (Exception e)
        {
            confirmationResult.AddMessage(e.Message, ResultMessageType.Error);
            confirmationResult.Success = false;
        }

        return confirmationResult;
    }

    public async Task<AuthenticationResult> ForgotPasswordAsync(ForgotPasswordParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var forgotPasswordResult = new AuthenticationResult();

        try
        {
            var user = await userManager.FindByEmailAsync(parameters.Email!);
            if (user == null || !await userManager.IsEmailConfirmedAsync(user))
            {
                // Don't reveal that the user does not exist or is not confirmed
                forgotPasswordResult.Success = true;
                forgotPasswordResult.NavigateToUrl = parameters.ReturnUrl;
                return forgotPasswordResult;
            }

            var code = await userManager.GeneratePasswordResetTokenAsync(user);
            var callbackUrl = $"{parameters.ReturnUrl}?userId={user.Id}&code={Uri.EscapeDataString(code)}";

            // Note: Email sending would need to be implemented without mediator
            logger.LogInformation("Password reset email would be sent to {Email} with callback URL: {CallbackUrl}", parameters.Email, callbackUrl);

            forgotPasswordResult.Success = true;
            forgotPasswordResult.NavigateToUrl = parameters.ReturnUrl;
        }
        catch (Exception e)
        {
            forgotPasswordResult.AddMessage(e.Message, ResultMessageType.Error);
            forgotPasswordResult.Success = false;
        }

        return forgotPasswordResult;
    }

    public async Task<AuthenticationResult> ResetPasswordAsync(ResetPasswordParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var resetPasswordResult = new AuthenticationResult();

        try
        {
            var user = await userManager.FindByIdAsync(parameters.UserId);
            if (user == null)
            {
                resetPasswordResult.AddMessage("User not found", ResultMessageType.Error);
                return resetPasswordResult;
            }

            var result = await userManager.ResetPasswordAsync(user, parameters.Code!, parameters.Password!);
            if (result.Succeeded)
            {
                resetPasswordResult.Success = true;
                resetPasswordResult.NavigateToUrl = parameters.ReturnUrl;
            }
            else
            {
                resetPasswordResult.Messages.AddRange(result.Errors.Select(e => new ResultMessage(e.Description, ResultMessageType.Error)));
            }
        }
        catch (Exception e)
        {
            resetPasswordResult.AddMessage(e.Message, ResultMessageType.Error);
            resetPasswordResult.Success = false;
        }

        return resetPasswordResult;
    }

    public async Task<User?> GetCurrentUserAsync(GetCurrentUserParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var authState = await authenticationStateProvider.GetAuthenticationStateAsync();
        
        return await userManager.GetUserAsync(authState.User);
    }

    private static string GenerateCacheKey(GetUserParameters parameters, IZauberDbContext dbContext)
    {
        var query = BuildQuery(parameters, dbContext);
        var queryString = query.ToQueryString();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(queryString));
        return typeof(User).ToCacheKey(Convert.ToBase64String(hash));
    }

    private static IQueryable<User> BuildQuery(GetUserParameters parameters, IZauberDbContext dbContext)
    {
        var query = dbContext.Users
            .Include(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .Include(x => x.PropertyData)
            .AsNoTracking()
            .AsSplitQuery()
            .Where(x => x.Id == parameters.Id);

        return query;
    }

    private static async Task<User?> FetchUserAsync(GetUserParameters parameters, IZauberDbContext dbContext, CancellationToken cancellationToken)
    {
        var query = BuildQuery(parameters, dbContext);
        return await query.FirstOrDefaultAsync(cancellationToken: cancellationToken);
    }

    private static IQueryable<User> BuildQuery(QueryUsersParameters parameters, IZauberDbContext dbContext)
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
        var query = BuildQuery(parameters, dbContext);
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
