using System.Security.Claims;
using ZauberCMS.Core.Membership.Models;

namespace ZauberCMS.Core.Membership.Interfaces;

/// <summary>
/// Who the current operation runs for. The Core services read the caller through this, for the audit trail and
/// "last updated by", never to decide whether a call is allowed: services trust their caller, and access is checked
/// by the page, component or endpoint that calls them (Roadmap D27). Register your own implementation after
/// <c>AddZauberCms</c> to take the caller from somewhere else (an API key, a job's own account).
/// </summary>
public interface ICurrentUserAccessor
{
    /// <summary>
    /// The caller: the principal set by <see cref="RunAs"/> on this async flow, else the Blazor authentication state
    /// (an admin circuit or a static page), else the current request's user (a controller or minimal API), else an
    /// anonymous principal (a hosted service). Never throws for want of a user.
    /// </summary>
    Task<ClaimsPrincipal> GetPrincipalAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// The caller's <see cref="User"/>, loaded fresh, or null when nobody is signed in, the account does not exist,
    /// or the caller was signed in by another scheme with an id that is not one of ours.
    /// </summary>
    Task<User?> GetUserAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs what follows on this async flow as <paramref name="principal"/> until the returned value is disposed:
    /// <c>using (currentUserAccessor.RunAs(principal)) { await contentService.SaveContentAsync(...); }</c>. For code
    /// with no signed-in user that should still be recorded as someone: the installer runs the starter kit as the
    /// new administrator; a job can run as an account of its own. It applies to the services of the scope it was
    /// called in and to scopes that have no Blazor render of their own (a job's); it never replaces the user of a
    /// circuit or page it reaches through an event. Call it from the method that awaits the work (a value set inside
    /// an awaited async method does not flow back to its caller) and dispose it there, nested calls in reverse order
    /// (<c>using</c> does both). Not for components: work a render starts inside the block keeps the value.
    /// </summary>
    IDisposable RunAs(ClaimsPrincipal principal);
}
