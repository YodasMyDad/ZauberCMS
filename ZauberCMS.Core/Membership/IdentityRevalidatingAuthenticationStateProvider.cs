using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZauberCMS.Core.Membership.Models;

namespace ZauberCMS.Core.Membership;

// This is a server-side AuthenticationStateProvider that revalidates the security stamp for the connected user
// every 30 minutes an interactive circuit is connected.
public sealed class IdentityRevalidatingAuthenticationStateProvider(
    ILoggerFactory loggerFactory,
    IServiceScopeFactory scopeFactory,
    IOptions<IdentityOptions> options)
    : RevalidatingServerAuthenticationStateProvider(loggerFactory)
{
    protected override TimeSpan RevalidationInterval => TimeSpan.FromMinutes(10);

    private readonly ILogger _logger = loggerFactory.CreateLogger<IdentityRevalidatingAuthenticationStateProvider>();

    protected override async Task<bool> ValidateAuthenticationStateAsync(
        AuthenticationState authenticationState, CancellationToken cancellationToken)
    {
        try
        {
            // Get the user manager from a new scope to ensure it fetches fresh data
            await using var scope = scopeFactory.CreateAsyncScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            return await ValidateSecurityStampAsync(userManager, authenticationState.User);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The check could not read the user (the database is down a moment). Signing the circuit out would send
            // the user to the login page and lose their unsaved work; the sign-in stays until the next check, ten
            // minutes on, and every request still checks the stamp itself (ZA-215, Roadmap D32)
            _logger.LogWarning(ex, "The sign-in of an open admin tab could not be checked; it is checked again later");
            return true;
        }
    }

    private async Task<bool> ValidateSecurityStampAsync(UserManager<User> userManager,
        ClaimsPrincipal principal)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null)
        {
            return false;
        }
        else if (!userManager.SupportsUserSecurityStamp)
        {
            return true;
        }
        else
        {
            var principalStamp = principal.FindFirstValue(options.Value.ClaimsIdentity.SecurityStampClaimType);
            var userStamp = await userManager.GetSecurityStampAsync(user);
            return principalStamp == userStamp;
        }
    }
}