using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace ZauberCMS.Components.Admin.Shared;

/// <summary>
/// Whether an admin extension (a nav item, a nav group action, a dashboard, a tree context menu) is shown to a user:
/// when its type carries no <c>[Authorize]</c> (or <c>[AuthorizeSection]</c>), always; otherwise when the user passes
/// it, the way a page is opened only to them (Roadmap D67, D70). A plugin limits its admin-only tool in a section that
/// other roles may open with <c>[Authorize(Roles = Constants.Roles.AdminRoleName)]</c> on the type.
/// </summary>
internal static class AdminExtensionAccess
{
    public static async Task<bool> MayUseAsync(Type extensionType, ClaimsPrincipal user,
        IAuthorizationService authorizationService, IAuthorizationPolicyProvider policyProvider)
    {
        var authorizeData = extensionType.GetCustomAttributes(inherit: true).OfType<IAuthorizeData>().ToList();
        if (authorizeData.Count == 0)
        {
            return true;
        }

        var policy = await AuthorizationPolicy.CombineAsync(policyProvider, authorizeData);
        return policy == null || (await authorizationService.AuthorizeAsync(user, policy)).Succeeded;
    }
}
