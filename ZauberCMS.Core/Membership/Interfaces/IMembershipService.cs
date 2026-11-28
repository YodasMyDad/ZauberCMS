using System.Threading;
using System.Threading.Tasks;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Membership.Parameters;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Membership.Interfaces;

public interface IMembershipService
{
    Task<User?> GetUserAsync(GetUserParameters parameters, CancellationToken cancellationToken = default);
    Task<HandlerResult<User>> SaveUserAsync(SaveUserParameters parameters, CancellationToken cancellationToken = default);
    Task<HandlerResult<User>> CreateUpdateUserAsync(CreateUpdateUserParameters parameters, CancellationToken cancellationToken = default);
    /// <summary>
    /// Deletes a user and erases the person (ZA-033, Roadmap D35). The row stays, marked deleted, so pages and versions
    /// still point at an author, but its user name becomes <c>deleted-&lt;id&gt;</c>, and its email, phone, password,
    /// profile values, logins, claims and roles are removed. The address and the old user name can register again.
    /// There is no restore. Audit rows keep the text they were written with until the daily job removes them.
    /// </summary>
    Task<HandlerResult<User>> DeleteUserAsync(DeleteUserParameters parameters, CancellationToken cancellationToken = default);
    Task<PaginatedList<User>> QueryUsersAsync(QueryUsersParameters parameters, CancellationToken cancellationToken = default);

    Task<Role?> GetRoleAsync(GetRoleParameters parameters, CancellationToken cancellationToken = default);
    Task<HandlerResult<Role>> SaveRoleAsync(SaveRoleParameters parameters, CancellationToken cancellationToken = default);
    Task<HandlerResult<Role>> DeleteRoleAsync(DeleteRoleParameters parameters, CancellationToken cancellationToken = default);
    Task<PaginatedList<Role>> QueryRolesAsync(QueryRolesParameters parameters, CancellationToken cancellationToken = default);

    Task<AuthenticationResult> LoginUserAsync(LoginUserParameters parameters, CancellationToken cancellationToken = default);
    Task<AuthenticationResult> RegisterUserAsync(RegisterUserParameters parameters, CancellationToken cancellationToken = default);
    Task<AuthenticationResult> ExternalLoginAsync(ExternalLoginParameters parameters, CancellationToken cancellationToken = default);
    Task<AuthenticationResult> ConfirmEmailAsync(ConfirmEmailParameters parameters, CancellationToken cancellationToken = default);
    Task<AuthenticationResult> ForgotPasswordAsync(ForgotPasswordParameters parameters, CancellationToken cancellationToken = default);
    Task<AuthenticationResult> ResetPasswordAsync(ResetPasswordParameters parameters, CancellationToken cancellationToken = default);
    Task<User?> GetCurrentUserAsync(CancellationToken cancellationToken = default);

    // A user's own account: what the admin's current-user panel changes (Roadmap D72). Like every service method
    // these check no access (D27): the caller decides whose account it changes; the panel only ever passes the
    // signed-in user's own id.

    /// <summary>
    /// Changes the user's name (<c>UserName</c>) and nothing else. The name is checked as any user name is (allowed
    /// characters, not taken). The security stamp is kept, so no session ends: users sign in with their email
    /// address, not their name, and a sign-in shows the new name once it is next renewed (within five minutes).
    /// Writes an audit row.
    /// </summary>
    Task<HandlerResult<User>> ChangeUserNameAsync(ChangeUserNameParameters parameters, CancellationToken cancellationToken = default);

    /// <summary>
    /// Changes the user's password when <see cref="ChangePasswordParameters.CurrentPassword"/> is right. A wrong one
    /// counts towards the account's lockout as a wrong password at sign-in does, and a locked account is refused.
    /// The new password must meet the site's password rules. The security stamp is replaced, so the user's other
    /// sessions end; when the caller is that user the result carries <see cref="HandlerResult{T}.RefreshSignIn"/>
    /// and a ticket, to renew the caller's own sign-in. Writes an audit row.
    /// </summary>
    Task<HandlerResult<User>> ChangePasswordAsync(ChangePasswordParameters parameters, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a link to <see cref="RequestEmailChangeParameters.NewEmailAddress"/>; the account moves to that address
    /// when the link is opened (<see cref="ConfirmEmailAsync"/>), which also ends the user's other sessions. Until
    /// then the user signs in with the old address. Needs the current password, checked as in
    /// <see cref="ChangePasswordAsync"/>; an account without a password is refused. At most one link per account per
    /// minute, and only the newest link works (a link sent before it, for an address the user replaced, is refused).
    /// Whether the new address belongs to another account is not said here (that would tell the caller who has an
    /// account); the link is then refused when it is opened. Writes an audit row, and another when the address
    /// moves. Like every write to a user's sign-in fields, these go through <c>UserManager</c>: the save hooks
    /// (<c>IBeforeEntitySave&lt;User&gt;</c> and the others) do not run for them.
    /// </summary>
    Task<HandlerResult<User>> RequestEmailChangeAsync(RequestEmailChangeParameters parameters, CancellationToken cancellationToken = default);
}