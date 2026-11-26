namespace ZauberCMS.Core.Content.Parameters;

/// <summary>
/// Public access ("restrict access"): the roles that may see a page, set on the page and every page below it.
/// </summary>
public class SetContentRolesParameters
{
    /// <summary>The page whose access is set.</summary>
    public Guid ContentId { get; set; }

    /// <summary>The roles that may see it; empty removes the restriction.</summary>
    public List<Guid> RoleIds { get; set; } = [];
}
