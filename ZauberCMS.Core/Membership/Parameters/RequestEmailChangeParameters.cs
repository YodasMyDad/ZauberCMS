namespace ZauberCMS.Core.Membership.Parameters;

/// <summary>
/// Asks to move a user's account to a new email address: see
/// <see cref="Interfaces.IMembershipService.RequestEmailChangeAsync"/>. The address changes only when the link
/// sent to the new address is opened.
/// </summary>
public class RequestEmailChangeParameters
{
    /// <summary>The user whose address changes.</summary>
    public Guid UserId { get; set; }

    /// <summary>The address to move to. The link is sent here.</summary>
    public string? NewEmailAddress { get; set; }

    /// <summary>
    /// The user's current password. Required: whoever has only a signed-in session cannot move the account to an
    /// address of their own and then reset its password. A wrong one counts towards the account's lockout.
    /// </summary>
    public string? CurrentPassword { get; set; }

    /// <summary>Carried in the emailed link as <c>returnUrl</c>, for a confirmation page of the site's own; the built-in page shows a link to sign in.</summary>
    public string? ReturnUrl { get; set; }
}
