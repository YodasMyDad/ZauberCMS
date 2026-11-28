using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZauberCMS.Core.Audit.Interfaces;
using ZauberCMS.Core.Audit.Parameters;
using ZauberCMS.Core.Email.Parameters;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Membership.Parameters;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Membership.Services;

// A user's own account: name, email address and password, as the admin's current-user panel changes them
// (Roadmap D72). No access check here (D27): the panel passes only the signed-in user's own id.
public partial class MembershipService
{
    /// <summary>How often <see cref="RequestEmailChangeAsync"/> sends a link for one account, on this server.</summary>
    internal static readonly TimeSpan EmailChangeLinkInterval = TimeSpan.FromMinutes(1);

    /// <summary>The key in <see cref="User.ExtendedData"/> of the address the newest email-change link is for.</summary>
    internal const string PendingEmailKey = "ZauberPendingEmail";

    /// <summary>The longest name the user table stores (<c>UserDbMapping</c>).</summary>
    internal const int MaxUserNameLength = 150;

    private static readonly SemaphoreSlim CurrentPasswordChecks = new(1, 1);

    /// <inheritdoc />
    public async Task<HandlerResult<User>> ChangeUserNameAsync(ChangeUserNameParameters parameters,
        CancellationToken cancellationToken = default)
    {
        var handlerResult = new HandlerResult<User>();
        var name = parameters.Name?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            handlerResult.Messages.Add(new ResultMessage(text["membership_enterName"], ResultMessageType.Error));
            return handlerResult;
        }

        if (name.Length > MaxUserNameLength)
        {
            handlerResult.Messages.Add(new ResultMessage(text["membership_nameTooLong", MaxUserNameLength], ResultMessageType.Error));
            return handlerResult;
        }

        using var scope = serviceScopeFactory.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var user = await FindAccountAsync(userManager, parameters.UserId);
        if (user == null)
        {
            handlerResult.Messages.Add(new ResultMessage(text["membership_userNotFound"], ResultMessageType.Error));
            return handlerResult;
        }

        handlerResult.Entity = user;
        if (string.Equals(user.UserName, name, StringComparison.Ordinal))
        {
            handlerResult.Success = true;
            return handlerResult;
        }

        var caller = await currentUserAccessor.GetUserAsync(cancellationToken);
        var oldName = user.UserName;
        // Not SetUserNameAsync, which replaces the security stamp and so ends every session of the user, the one
        // making the change included: the name is not what anybody signs in with (that is the email address).
        // UpdateAsync still checks the name as Identity checks every user name (allowed characters, not taken).
        user.UserName = name;
        user.DateUpdated = DateTime.UtcNow;
        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            handlerResult.Messages.AddRange(result.Errors.Select(e => new ResultMessage(e.Description, ResultMessageType.Error)));
            return handlerResult;
        }

        cacheService.ClearCachedItemsWithPrefix(nameof(User));
        await WriteAccountAuditAsync(scope.ServiceProvider, caller, caller?.Id == user.Id
            ? $"changed their name from {oldName} to {name}"
            : $"changed the name of {oldName} to {name}", cancellationToken);
        handlerResult.Success = true;
        return handlerResult;
    }

    /// <inheritdoc />
    public async Task<HandlerResult<User>> ChangePasswordAsync(ChangePasswordParameters parameters,
        CancellationToken cancellationToken = default)
    {
        var handlerResult = new HandlerResult<User>();
        if (string.IsNullOrEmpty(parameters.NewPassword))
        {
            handlerResult.Messages.Add(new ResultMessage(text["membership_enterNewPassword"], ResultMessageType.Error));
            return handlerResult;
        }

        using var scope = serviceScopeFactory.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        User? user;
        // One at a time, loaded inside the lock: two tries at once would each count from the same stored number of
        // wrong passwords, and one of the counts would be lost
        await CurrentPasswordChecks.WaitAsync(cancellationToken);
        try
        {
            user = await FindAccountAsync(userManager, parameters.UserId);
            if (user == null)
            {
                handlerResult.Messages.Add(new ResultMessage(text["membership_userNotFound"], ResultMessageType.Error));
                return handlerResult;
            }

            var refused = await CheckCurrentPasswordAsync(userManager, user, parameters.CurrentPassword);
            if (refused != null)
            {
                handlerResult.Messages.Add(new ResultMessage(refused, ResultMessageType.Error));
                return handlerResult;
            }

            // Checks the new password against the site's rules and replaces the security stamp: the user's other
            // sessions end at their next check (five minutes for requests, ten for an open admin tab)
            var result = await userManager.ChangePasswordAsync(user, parameters.CurrentPassword!, parameters.NewPassword);
            if (!result.Succeeded)
            {
                handlerResult.Messages.AddRange(result.Errors.Select(e => new ResultMessage(e.Description, ResultMessageType.Error)));
                return handlerResult;
            }
        }
        finally
        {
            CurrentPasswordChecks.Release();
        }

        cacheService.ClearCachedItemsWithPrefix(nameof(User));
        var caller = await currentUserAccessor.GetUserAsync(cancellationToken);
        await WriteAccountAuditAsync(scope.ServiceProvider, caller, caller?.Id == user.Id
            ? "changed their password"
            : $"changed the password of {user.UserName}", cancellationToken);
        logger.LogInformation("The password of user {UserId} was changed", user.Id);

        handlerResult.Entity = user;
        handlerResult.Success = true;
        if (caller?.Id == user.Id)
        {
            // The caller's own cookie carries the old stamp: renew it, or they are signed out too
            handlerResult.RefreshSignIn = true;
            handlerResult.RefreshSignInTicket =
                scope.ServiceProvider.GetRequiredService<RefreshSignInTicketService>().Create(user);
        }

        return handlerResult;
    }

    /// <inheritdoc />
    public async Task<HandlerResult<User>> RequestEmailChangeAsync(RequestEmailChangeParameters parameters,
        CancellationToken cancellationToken = default)
    {
        var handlerResult = new HandlerResult<User>();
        var newEmail = parameters.NewEmailAddress?.Trim();
        if (string.IsNullOrEmpty(newEmail) || newEmail.Length > 256 || !new EmailAddressAttribute().IsValid(newEmail))
        {
            handlerResult.Messages.Add(new ResultMessage(text["membership_enterValidEmail"], ResultMessageType.Error));
            return handlerResult;
        }

        using var scope = serviceScopeFactory.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var memoryCache = scope.ServiceProvider.GetRequiredService<IMemoryCache>();
        User? user;
        string sentKey;
        // As for a password change: one check at a time, the account loaded inside the lock
        await CurrentPasswordChecks.WaitAsync(cancellationToken);
        try
        {
            user = await FindAccountAsync(userManager, parameters.UserId);
            if (user == null)
            {
                handlerResult.Messages.Add(new ResultMessage(text["membership_userNotFound"], ResultMessageType.Error));
                return handlerResult;
            }

            handlerResult.Entity = user;
            if (string.Equals(user.Email, newEmail, StringComparison.OrdinalIgnoreCase))
            {
                handlerResult.Messages.Add(new ResultMessage(text["membership_alreadyAccountEmail"], ResultMessageType.Error));
                return handlerResult;
            }

            var refused = await CheckCurrentPasswordAsync(userManager, user, parameters.CurrentPassword);
            if (refused != null)
            {
                handlerResult.Messages.Add(new ResultMessage(refused, ResultMessageType.Error));
                return handlerResult;
            }

            // Not a way to fill someone's inbox: one link per account per minute on this server
            sentKey = $"ZauberCMS.EmailChangeLinkSent.{user.Id}";
            if (memoryCache.TryGetValue(sentKey, out _))
            {
                handlerResult.Messages.Add(new ResultMessage(text["membership_emailChangeLinkTooSoon"],
                    ResultMessageType.Error));
                return handlerResult;
            }

            memoryCache.Set(sentKey, true, EmailChangeLinkInterval);

            // Only the address asked for last can be confirmed: a link sent before (an address mistyped, then
            // corrected) stops working, so its reader cannot take the account (ConfirmEmailAsync checks it)
            user.ExtendedData[PendingEmailKey] = newEmail;
            var marked = await userManager.UpdateAsync(user);
            if (!marked.Succeeded)
            {
                memoryCache.Remove(sentKey);
                handlerResult.Messages.AddRange(marked.Errors.Select(e => new ResultMessage(e.Description, ResultMessageType.Error)));
                return handlerResult;
            }
        }
        finally
        {
            CurrentPasswordChecks.Release();
        }

        try
        {
            await emailService.SendEmailConfirmationAsync(new SendEmailConfirmationParameters
            {
                User = user,
                NewEmailAddress = newEmail,
                ReturnUrl = parameters.ReturnUrl
            }, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            memoryCache.Remove(sentKey);
            logger.LogError(ex, "Could not send the email-change link for user {UserId}", user.Id);
            handlerResult.Messages.Add(new ResultMessage(text["membership_emailNotSent"], ResultMessageType.Error));
            return handlerResult;
        }

        var caller = await currentUserAccessor.GetUserAsync(cancellationToken);
        // Not the address itself: audit rows are kept for months, and the address may never be confirmed
        await WriteAccountAuditAsync(scope.ServiceProvider, caller, caller?.Id == user.Id
            ? "asked to change their email address"
            : $"asked to change the email address of {user.UserName}", cancellationToken);
        handlerResult.Success = true;
        return handlerResult;
    }

    private static async Task<User?> FindAccountAsync(UserManager<User> userManager, Guid userId)
    {
        if (userId == Guid.Empty)
        {
            return null;
        }

        var user = await userManager.FindByIdAsync(userId.ToString());
        return user is { Deleted: false } ? user : null;
    }

    /// <summary>
    /// Null when <paramref name="password"/> is the user's password, else why not. A wrong one counts as a wrong
    /// password at sign-in does (six lock the account for five minutes), so the panel cannot be used to guess it.
    /// </summary>
    private async Task<string?> CheckCurrentPasswordAsync(UserManager<User> userManager, User user,
        string? password)
    {
        if (await userManager.IsLockedOutAsync(user))
        {
            return text["membership_lockedWrongPasswords"];
        }

        if (!await userManager.HasPasswordAsync(user))
        {
            return text["membership_noPasswordExternalLogin"];
        }

        if (string.IsNullOrEmpty(password))
        {
            return text["membership_enterCurrentPassword"];
        }

        if (!await userManager.CheckPasswordAsync(user, password))
        {
            await userManager.AccessFailedAsync(user);
            return await userManager.IsLockedOutAsync(user)
                ? text["membership_lockedWrongPasswords"].Value
                : text["membership_currentPasswordWrong"].Value;
        }

        await userManager.ResetAccessFailedCountAsync(user);
        return null;
    }

    /// <summary>
    /// An audit row "&lt;caller&gt; &lt;what&gt;", for a known caller only, as every audit row. The change it records is
    /// already stored, so a row that cannot be written is logged, not thrown: the caller still gets its result (and
    /// a password change its sign-in renewal).
    /// </summary>
    private async Task WriteAccountAuditAsync(IServiceProvider services, User? caller, string what,
        CancellationToken cancellationToken)
    {
        if (caller == null)
        {
            return;
        }

        try
        {
            await services.GetRequiredService<IAuditService>().SaveAuditAsync(new SaveAuditParameters
            {
                Audit = new Audit.Models.Audit { Description = $"{caller.Name} {what}" }
            }, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "The audit row \"{Caller} {What}\" could not be written", caller.Name, what);
        }
    }
}
