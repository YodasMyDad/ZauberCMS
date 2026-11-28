namespace ZauberCMS.Core.Membership.Parameters;

/// <summary>
/// Changes a user's password when the current one is known: see
/// <see cref="Interfaces.IMembershipService.ChangePasswordAsync"/>. Setting a password without the current one (an
/// administrator for another user, or an emailed reset link) is a reset, through <c>UserManager</c> or
/// <see cref="Interfaces.IMembershipService.ResetPasswordAsync"/>.
/// </summary>
public class ChangePasswordParameters
{
    /// <summary>The user whose password changes.</summary>
    public Guid UserId { get; set; }

    /// <summary>The password the user has now. A wrong one counts towards the account's lockout, as at sign-in.</summary>
    public string? CurrentPassword { get; set; }

    /// <summary>The new password; it must meet the site's password rules (<c>Zauber:Identity:Password*</c>).</summary>
    public string? NewPassword { get; set; }
}
