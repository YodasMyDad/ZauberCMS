namespace ZauberCMS.Core.Sections.Authorization;

/// <summary>
/// The authorisation policies of the admin's sections (Roadmap D38, D67). ZauberCMS's policy provider
/// (<see cref="SectionAuthorizationPolicyProvider"/>) builds them from their names, so they need no registration:
/// <c>[Authorize(Policy = SectionPolicies.ForSections("MySection"))]</c>, or the shorter
/// <see cref="AuthorizeSectionAttribute"/>, on a page, a controller or an action; <c>RequireAuthorization(...)</c> on a
/// minimal API; <c>&lt;AuthorizeView Policy="..."&gt;</c> in a component.
/// </summary>
public static class SectionPolicies
{
    /// <summary>The start of a section policy's name; the section aliases follow, separated by commas.</summary>
    public const string SectionPrefix = "ZauberCMS.Section:";

    /// <summary>
    /// Signed in and allowed to open at least one section of the admin: what the admin's own landing page
    /// (<c>/admin</c>) asks for.
    /// </summary>
    public const string BackOffice = "ZauberCMS.BackOffice";

    /// <summary>
    /// The name of the policy that lets a user in who may open any one of <paramref name="sectionAliases"/>
    /// (<c>ISection.Alias</c>). With no alias it is <see cref="BackOffice"/>. An administrator always passes.
    /// </summary>
    public static string ForSections(params string[] sectionAliases)
    {
        var aliases = Normalise(sectionAliases);
        return aliases.Count == 0 ? BackOffice : SectionPrefix + string.Join(',', aliases);
    }

    /// <summary>
    /// The section aliases a policy name asks for: none for <see cref="BackOffice"/> (any section), null for a name
    /// that is not a section policy.
    /// </summary>
    public static IReadOnlyList<string>? ParseSections(string? policyName)
    {
        if (string.Equals(policyName, BackOffice, StringComparison.Ordinal))
        {
            return [];
        }

        if (policyName == null || !policyName.StartsWith(SectionPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        return Normalise(policyName[SectionPrefix.Length..].Split(','));
    }

    private static List<string> Normalise(IEnumerable<string?>? aliases) =>
        (aliases ?? [])
        .Where(alias => !string.IsNullOrWhiteSpace(alias))
        .Select(alias => alias!.Trim())
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();
}
