using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using ZauberCMS.Core.Membership.Models;

namespace ZauberCMS.Core.Membership;

/// <summary>
/// Short-lived proof that the signed-in user has just changed their own account (user name, email, roles) and
/// may have their sign-in cookie re-issued.
/// A change of user name or email replaces the security stamp, so the cookie the user holds no longer validates.
/// The ticket is bound to the user and to the stamp that change produced; without it the refresh endpoint only
/// re-issues a cookie whose stamp is still valid. A stolen cookie therefore cannot be renewed after the account's
/// password is reset or the user is deleted.
/// </summary>
public class RefreshSignInTicketService(IDataProtectionProvider dataProtectionProvider)
{
    /// <summary>How long a ticket is accepted. It is used by the redirect that follows the save.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(2);

    private readonly ITimeLimitedDataProtector _protector = dataProtectionProvider
        .CreateProtector("ZauberCMS.Membership.RefreshSignIn.v1")
        .ToTimeLimitedDataProtector();

    /// <summary>Creates a ticket for the user's current security stamp.</summary>
    public string Create(User user) => _protector.Protect(Payload(user), Lifetime);

    /// <summary>True when the ticket was issued for this user and the user's stamp has not changed since.</summary>
    public bool IsValid(string? ticket, User user)
    {
        if (string.IsNullOrWhiteSpace(ticket))
        {
            return false;
        }

        try
        {
            return string.Equals(_protector.Unprotect(ticket), Payload(user), StringComparison.Ordinal);
        }
        catch (CryptographicException)
        {
            // Tampered with, expired, or protected with other keys.
            return false;
        }
    }

    private static string Payload(User user) => $"{user.Id:N}|{user.SecurityStamp}";
}
