using Microsoft.AspNetCore.Authorization;
using ZauberCMS.Core.Sections.Interfaces;

namespace ZauberCMS.Core.Sections.Authorization;

/// <summary>Meets <see cref="SectionAccessRequirement"/> by asking <see cref="IBackOfficeAccessService"/>.</summary>
internal sealed class SectionAccessHandler(IBackOfficeAccessService backOfficeAccess)
    : AuthorizationHandler<SectionAccessRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,
        SectionAccessRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return;
        }

        if (requirement.SectionAliases.Count == 0)
        {
            if ((await backOfficeAccess.GetSectionsAsync(context.User)).Count > 0)
            {
                context.Succeed(requirement);
            }

            return;
        }

        foreach (var alias in requirement.SectionAliases)
        {
            if (await backOfficeAccess.CanOpenSectionAsync(context.User, alias))
            {
                context.Succeed(requirement);
                return;
            }
        }
    }
}
