using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;
using ZauberCMS.Core.Localization;

namespace ZauberCMS.Core.Membership.Validation;

/// <summary>
/// ASP.NET Identity's own messages (the password rules, a name or address already taken, a wrong password) in
/// ZauberCMS's text, so they follow the culture as the rest of the page does: the request's on the account pages, the
/// admin's in the admin (Roadmap D131). Each error keeps Identity's code; the English is Identity's. Umbraco does the
/// same with its <c>BackOfficeErrorDescriber</c> and <c>MembersErrorDescriber</c>. A site with messages of its own
/// registers its describer with <c>AddErrorDescriber</c> after <c>AddZauberCms</c> (the last registration wins); a
/// message Identity adds in a later version and this class does not override stays Identity's English.
/// </summary>
internal sealed class ZauberIdentityErrorDescriber(IStringLocalizer<ZauberText> text) : IdentityErrorDescriber
{
    public override IdentityError DefaultError() =>
        Localised(base.DefaultError(), text["membership_identityDefaultError"]);

    public override IdentityError ConcurrencyFailure() =>
        Localised(base.ConcurrencyFailure(), text["membership_identityConcurrencyFailure"]);

    public override IdentityError PasswordMismatch() =>
        Localised(base.PasswordMismatch(), text["membership_identityPasswordMismatch"]);

    public override IdentityError InvalidToken() =>
        Localised(base.InvalidToken(), text["membership_identityInvalidToken"]);

    public override IdentityError RecoveryCodeRedemptionFailed() =>
        Localised(base.RecoveryCodeRedemptionFailed(), text["membership_identityRecoveryCodeRedemptionFailed"]);

    public override IdentityError LoginAlreadyAssociated() =>
        Localised(base.LoginAlreadyAssociated(), text["membership_identityLoginAlreadyAssociated"]);

    public override IdentityError InvalidUserName(string? userName) =>
        Localised(base.InvalidUserName(userName), text["membership_identityInvalidUserName", userName ?? string.Empty]);

    public override IdentityError InvalidEmail(string? email) =>
        Localised(base.InvalidEmail(email), text["membership_identityInvalidEmail", email ?? string.Empty]);

    public override IdentityError DuplicateUserName(string userName) =>
        Localised(base.DuplicateUserName(userName), text["membership_identityDuplicateUserName", userName]);

    public override IdentityError DuplicateEmail(string email) =>
        Localised(base.DuplicateEmail(email), text["membership_identityDuplicateEmail", email]);

    public override IdentityError InvalidRoleName(string? role) =>
        Localised(base.InvalidRoleName(role), text["membership_identityInvalidRoleName", role ?? string.Empty]);

    public override IdentityError DuplicateRoleName(string role) =>
        Localised(base.DuplicateRoleName(role), text["membership_identityDuplicateRoleName", role]);

    public override IdentityError UserAlreadyHasPassword() =>
        Localised(base.UserAlreadyHasPassword(), text["membership_identityUserAlreadyHasPassword"]);

    public override IdentityError UserLockoutNotEnabled() =>
        Localised(base.UserLockoutNotEnabled(), text["membership_identityUserLockoutNotEnabled"]);

    public override IdentityError UserAlreadyInRole(string role) =>
        Localised(base.UserAlreadyInRole(role), text["membership_identityUserAlreadyInRole", role]);

    public override IdentityError UserNotInRole(string role) =>
        Localised(base.UserNotInRole(role), text["membership_identityUserNotInRole", role]);

    public override IdentityError PasswordTooShort(int length) =>
        Localised(base.PasswordTooShort(length), text["membership_identityPasswordTooShort", length]);

    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) =>
        Localised(base.PasswordRequiresUniqueChars(uniqueChars),
            text["membership_identityPasswordRequiresUniqueChars", uniqueChars]);

    public override IdentityError PasswordRequiresNonAlphanumeric() =>
        Localised(base.PasswordRequiresNonAlphanumeric(), text["membership_identityPasswordRequiresNonAlphanumeric"]);

    public override IdentityError PasswordRequiresDigit() =>
        Localised(base.PasswordRequiresDigit(), text["membership_identityPasswordRequiresDigit"]);

    public override IdentityError PasswordRequiresLower() =>
        Localised(base.PasswordRequiresLower(), text["membership_identityPasswordRequiresLower"]);

    public override IdentityError PasswordRequiresUpper() =>
        Localised(base.PasswordRequiresUpper(), text["membership_identityPasswordRequiresUpper"]);

    // Identity's code, so code that checks it (DuplicateEmail, PasswordTooShort, ...) works as before
    private static IdentityError Localised(IdentityError identity, string description) =>
        new() { Code = identity.Code, Description = description };
}
