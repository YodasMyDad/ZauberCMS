using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Membership.Interfaces;
using ZauberCMS.Core.Membership.Models;

namespace ZauberCMS.Core.Membership.Services;

/// <summary>
/// The default <see cref="ICurrentUserAccessor"/>. The services used to read <see cref="AuthenticationStateProvider"/>
/// directly, which only has a state inside a Blazor render: a save from a controller, an API or a hosted service
/// threw (ZA-093).
/// </summary>
public class CurrentUserAccessor(
    AuthenticationStateProvider authenticationStateProvider,
    IHttpContextAccessor httpContextAccessor,
    IServiceScopeFactory serviceScopeFactory) : ICurrentUserAccessor
{
    // Per async flow, not per scope: a circuit is one scope shared by every event the user raises in it. The owner
    // keeps it from crossing into another circuit: AppState handlers run on the publisher's flow, so a value set
    // there reaches every circuit's handlers.
    private sealed record RunAsEntry(ClaimsPrincipal Principal, CurrentUserAccessor Owner);

    private static readonly AsyncLocal<RunAsEntry?> RunAsPrincipal = new();

    /// <inheritdoc />
    public virtual async Task<ClaimsPrincipal> GetPrincipalAsync(CancellationToken cancellationToken = default)
    {
        var runAs = RunAsPrincipal.Value;
        if (runAs != null && ReferenceEquals(runAs.Owner, this))
        {
            return runAs.Principal;
        }

        try
        {
            // A scope with a render of its own (a circuit, a static page) keeps its user, whatever another set
            return (await authenticationStateProvider.GetAuthenticationStateAsync()).User;
        }
        catch (InvalidOperationException)
        {
            // Nothing rendered a component in this scope, so nobody set the state (the built-in provider throws)
        }

        // A scope with no render (a job's own scope) runs as whoever the flow runs as
        return runAs?.Principal ?? httpContextAccessor.HttpContext?.User ?? new ClaimsPrincipal(new ClaimsIdentity());
    }

    /// <inheritdoc />
    public virtual async Task<User?> GetUserAsync(CancellationToken cancellationToken = default)
    {
        var principal = await GetPrincipalAsync(cancellationToken);

        // A UserManager (and context) of its own: one held by a circuit keeps returning the user as it was first
        // loaded (ZA-086)
        using var scope = serviceScopeFactory.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

        // Only one of our accounts: a request signed in by another scheme (a bearer token) may carry any id, and
        // the store throws on one that is not a GUID
        return Guid.TryParse(userManager.GetUserId(principal), out _)
            ? await userManager.GetUserAsync(principal)
            : null;
    }

    /// <inheritdoc />
    public virtual IDisposable RunAs(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);
        var previous = RunAsPrincipal.Value;
        var entry = new RunAsEntry(principal, this);
        RunAsPrincipal.Value = entry;
        return new Restore(entry, previous);
    }

    private sealed class Restore(RunAsEntry entry, RunAsEntry? previous) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            // Only on the flow that set it, and only while it is the innermost: never clobber another flow's value
            if (ReferenceEquals(RunAsPrincipal.Value, entry))
            {
                RunAsPrincipal.Value = previous;
            }
        }
    }
}
