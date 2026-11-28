using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using ZauberCMS.Core.Settings;

namespace ZauberCMS.Core.Security.Services;

/// <summary>
/// The default <see cref="ZauberSetup.RegisterRateLimitPolicy"/> policy of the registration page: a sliding window per
/// visitor address from <c>Zauber:Identity:RegisterRateLimit</c>, 5 posts in 10 minutes by default. Only the posts
/// count: looking at the form is free, sending it is not. A limit of its own, so a flood of sign-ups neither uses up
/// the sign-in allowance of the people behind the same address nor hides inside it. A <c>PermitLimit</c> of 0 turns
/// it off.
/// </summary>
/// <remarks>Replaceable as <see cref="LoginRateLimiterPolicy"/> is, under its own name (Roadmap D104).</remarks>
public class RegisterRateLimiterPolicy(IOptions<ZauberSettings> settings) : IRateLimiterPolicy<string>
{
    /// <inheritdoc />
    /// <remarks>None: a refused request gets <c>RateLimiterOptions.RejectionStatusCode</c>, 429.</remarks>
    public virtual Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected => null;

    /// <inheritdoc />
    public virtual RateLimitPartition<string> GetPartition(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        var limit = settings.Value.Identity.RegisterRateLimit;
        return HttpMethods.IsPost(httpContext.Request.Method) && limit.PermitLimit > 0
            ? RateLimitPartition.GetSlidingWindowLimiter(
                httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => limit.ToLimiterOptions())
            : RateLimitPartition.GetNoLimiter("register-view");
    }
}
