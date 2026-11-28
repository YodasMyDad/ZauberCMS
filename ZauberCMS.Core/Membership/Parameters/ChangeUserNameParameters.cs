namespace ZauberCMS.Core.Membership.Parameters;

/// <summary>
/// Changes the name of a user (<c>User.UserName</c>, which the admin shows as "Name") and nothing else: see
/// <see cref="Interfaces.IMembershipService.ChangeUserNameAsync"/>.
/// </summary>
public class ChangeUserNameParameters
{
    /// <summary>The user whose name changes.</summary>
    public Guid UserId { get; set; }

    /// <summary>The new name. Leading and trailing spaces are removed; it must not be empty or taken.</summary>
    public string? Name { get; set; }
}
