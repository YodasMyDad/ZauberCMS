using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using ZauberCMS.Core.Settings;

namespace ZauberCMS.Core.Security.Services;

/// <summary>
/// The default <see cref="ZauberSetup.LoginRateLimitPolicy"/> policy of the sign-in pages (login, external sign-in,
/// forgot and reset password): a sliding window per visitor address from <c>Zauber:Identity:LoginRateLimit</c>, 10
/// requests in 5 minutes by default, refused ones included (ZA-149). A <c>PermitLimit</c> of 0 turns it off. Behind a
/// proxy the address is the proxy's unless <c>Zauber:ForwardedHeaders</c> is set.
/// </summary>
/// <remarks>
/// Replaceable (Roadmap D104): ZauberCMS adds the policy under its name from the keyed
/// <see cref="IRateLimiterPolicy{TPartitionKey}"/> of <see cref="string"/> registered with that name, so a site
/// registers its own (<c>builder.Services.AddKeyedSingleton&lt;IRateLimiterPolicy&lt;string&gt;,
/// MyPolicy&gt;(ZauberSetup.LoginRateLimitPolicy)</c>), or subclasses this one and overrides
/// <see cref="GetPartition"/>.
/// </remarks>
public class LoginRateLimiterPolicy(IOptions<ZauberSettings> settings) : IRateLimiterPolicy<string>
{
    /// <inheritdoc />
    /// <remarks>None: a refused request gets <c>RateLimiterOptions.RejectionStatusCode</c>, 429.</remarks>
    public virtual Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected => null;

    /// <inheritdoc />
    public virtual RateLimitPartition<string> GetPartition(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        var limit = settings.Value.Identity.LoginRateLimit;
        return limit.PermitLimit > 0
            ? RateLimitPartition.GetSlidingWindowLimiter(
                httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => limit.ToLimiterOptions())
            : RateLimitPartition.GetNoLimiter("login-off");
    }
}
