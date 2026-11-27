using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using ZauberCMS.Core.Membership.Models;

namespace ZauberCMS.Core.Membership;

/// <summary>
/// The signed-in user, read from <see cref="AuthenticationStateProvider"/>: right only inside a Blazor render.
/// Replaced by <see cref="Interfaces.ICurrentUserAccessor"/> (Phase 10a, D27), which also works on static pages, in
/// controllers and in jobs. Still registered so a site that injects it keeps working; removed in a later major
/// (ZA-121).
/// </summary>
[Obsolete("Use ICurrentUserAccessor.GetUserAsync, which also works outside a Blazor render. " +
          "IdentityUserAccessor will be removed in a later major version.")]
public sealed class IdentityUserAccessor(
    UserManager<User> userManager,
    AuthenticationStateProvider authenticationStateProvider)
{
    public async Task<User?> GetRequiredUserAsync()
    {
        var authState = await authenticationStateProvider.GetAuthenticationStateAsync();
        var user = await userManager.GetUserAsync(authState.User);
        return user;
    }
}