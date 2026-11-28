using System.Collections.Concurrent;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace ZauberCMS.Core.Sections.Authorization;

/// <summary>
/// The authorisation policy provider <c>AddZauberCms</c> registers: ASP.NET's default provider, plus the
/// <see cref="SectionPolicies"/> policies, built from their names (Roadmap D67), so a plugin section needs no policy
/// registration. Every other policy comes from <c>AuthorizationOptions</c> as before.
/// <para>
/// A site that registers an <see cref="IAuthorizationPolicyProvider"/> of its own replaces this one, and the admin's
/// pages then fail with "policy not found": derive the site's provider from this class (or ask it first for the
/// names <see cref="SectionPolicies.ParseSections"/> recognises).
/// </para>
/// </summary>
public class SectionAuthorizationPolicyProvider(IOptions<AuthorizationOptions> options)
    : DefaultAuthorizationPolicyProvider(options)
{
    private readonly ConcurrentDictionary<string, AuthorizationPolicy> _sectionPolicies = new(StringComparer.Ordinal);

    // The same name always gives the same policy, so the authorisation middleware may cache it per endpoint
    public override bool AllowsCachingPolicies => true;

    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        // A policy the site registered under the same name wins
        var registered = await base.GetPolicyAsync(policyName);
        if (registered != null)
        {
            return registered;
        }

        var sections = SectionPolicies.ParseSections(policyName);
        if (sections == null)
        {
            return null;
        }

        return _sectionPolicies.GetOrAdd(policyName, static (_, aliases) => new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(new SectionAccessRequirement(aliases))
            .Build(), sections);
    }
}
