using Microsoft.AspNetCore.Authorization;

namespace ZauberCMS.Core.Sections.Authorization;

/// <summary>
/// Lets in a user who may open any one of the named admin sections (<c>ISection.Alias</c>); an administrator always
/// (Roadmap D38, D67). Put it on an admin page (<c>@attribute [AuthorizeSection("MySection")]</c>), a controller or an
/// action. With no alias it asks for any section at all (<see cref="SectionPolicies.BackOffice"/>).
/// <para>
/// It is an <see cref="AuthorizeAttribute"/> with a <see cref="SectionPolicies"/> policy, so the request pipeline,
/// Blazor's router and <c>AdminAccessMiddleware</c> treat it as the page's own authorisation. Several on one type
/// must all pass.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class AuthorizeSectionAttribute : AuthorizeAttribute
{
    public AuthorizeSectionAttribute(params string[] sectionAliases)
        : base(SectionPolicies.ForSections(sectionAliases))
    {
        SectionAliases = SectionPolicies.ParseSections(Policy) ?? [];
    }

    /// <summary>The sections named; empty means any section.</summary>
    public IReadOnlyList<string> SectionAliases { get; }
}
