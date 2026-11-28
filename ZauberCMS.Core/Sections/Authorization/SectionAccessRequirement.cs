using Microsoft.AspNetCore.Authorization;

namespace ZauberCMS.Core.Sections.Authorization;

/// <summary>
/// Met when the user may open any one of <see cref="SectionAliases"/>, or any section at all when it is empty
/// (Roadmap D67). The decision is <c>IBackOfficeAccessService</c>'s, so a site that replaces that service changes
/// it everywhere. Use it in a policy of your own with <c>policy.AddRequirements(new SectionAccessRequirement(...))</c>.
/// </summary>
public sealed class SectionAccessRequirement(IEnumerable<string> sectionAliases) : IAuthorizationRequirement
{
    /// <summary>The sections, any one of which lets the user in; empty means any section.</summary>
    public IReadOnlyList<string> SectionAliases { get; } = sectionAliases.ToList();

    public override string ToString() => SectionAliases.Count == 0
        ? "SectionAccessRequirement: any admin section"
        : $"SectionAccessRequirement: one of the admin sections {string.Join(", ", SectionAliases)}";
}
