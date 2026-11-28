using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Security.Services;
using ZauberCMS.Core.Tests.Infrastructure;

namespace ZauberCMS.Core.Tests.Security;

/// <summary>
/// ZP-003 (Roadmap D104): the <c>login</c> and <c>register</c> rate-limit policies the account pages name are the
/// keyed <see cref="IRateLimiterPolicy{TPartitionKey}"/> of each name, so a site replaces either by registering its
/// own under that name; the defaults keep reading <c>Zauber:Identity:LoginRateLimit</c> / <c>RegisterRateLimit</c>.
/// </summary>
public class RateLimitPolicyTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ZP003_without_a_policy_of_its_own_a_site_has_the_two_defaults()
    {
        await using var app = TestSite.BuildApp("", "", TestSite.CreateContentRoot(), validateContainer: false);

        Assert.IsType<LoginRateLimiterPolicy>(
            app.Services.GetRequiredKeyedService<IRateLimiterPolicy<string>>(ZauberSetup.LoginRateLimitPolicy));
        Assert.IsType<RegisterRateLimiterPolicy>(
            app.Services.GetRequiredKeyedService<IRateLimiterPolicy<string>>(ZauberSetup.RegisterRateLimitPolicy));
    }

    [Fact]
    public async Task ZP003_a_sites_own_policies_registered_after_AddZauberCms_limit_the_account_pages()
    {
        var contentRoot = TestSite.CreateContentRoot();
        await using var app = TestSite.BuildApp("", "", contentRoot, validateContainer: false,
            new Dictionary<string, string?> { ["urls"] = "http://127.0.0.1:0" },
            afterAddZauberCms: builder =>
            {
                builder.Services.AddKeyedSingleton<IRateLimiterPolicy<string>, OnePerClientPolicy>(
                    ZauberSetup.LoginRateLimitPolicy);
                builder.Services.AddKeyedSingleton<IRateLimiterPolicy<string>, OnePerClientPolicy>(
                    ZauberSetup.RegisterRateLimitPolicy);
            });
        app.UseRateLimiter();
        app.MapGet("/test/login", () => "ok").RequireRateLimiting(ZauberSetup.LoginRateLimitPolicy);
        app.MapGet("/test/register", () => "ok").RequireRateLimiting(ZauberSetup.RegisterRateLimitPolicy);
        await app.StartAsync(Ct);
        try
        {
            using var client = new HttpClient
            {
                BaseAddress = new Uri(app.Services.GetRequiredService<IServer>().Features
                    .Get<IServerAddressesFeature>()!.Addresses.First())
            };

            // The site's policy: one request per client header, where the default allows 10 per address
            Assert.Equal(HttpStatusCode.OK, (await SendAsync(client, "/test/login", "a")).StatusCode);
            Assert.Equal(HttpStatusCode.TooManyRequests, (await SendAsync(client, "/test/login", "a")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await SendAsync(client, "/test/login", "b")).StatusCode);
            // The default registration policy lets every look at the form through; the site's counts them
            Assert.Equal(HttpStatusCode.OK, (await SendAsync(client, "/test/register", "a")).StatusCode);
            Assert.Equal(HttpStatusCode.TooManyRequests, (await SendAsync(client, "/test/register", "a")).StatusCode);
        }
        finally
        {
            await app.StopAsync(Ct);
        }
    }

    [Fact]
    public async Task ZP003_a_policy_registered_before_AddZauberCms_is_kept()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Testing",
            ContentRootPath = TestSite.CreateContentRoot()
        });
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Zauber:DatabaseProvider"] = "",
            ["Zauber:ConnectionString"] = ""
        });
        builder.Services.AddKeyedSingleton<IRateLimiterPolicy<string>, OnePerClientPolicy>(
            ZauberSetup.LoginRateLimitPolicy);

        builder.AddZauberCms();
        await using var app = builder.Build();

        Assert.IsType<OnePerClientPolicy>(
            app.Services.GetRequiredKeyedService<IRateLimiterPolicy<string>>(ZauberSetup.LoginRateLimitPolicy));
        Assert.IsType<RegisterRateLimiterPolicy>(
            app.Services.GetRequiredKeyedService<IRateLimiterPolicy<string>>(ZauberSetup.RegisterRateLimitPolicy));
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, string path, string clientId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("X-Client", clientId);
        return await client.SendAsync(request, Ct);
    }

    /// <summary>A site's own policy: one request per <c>X-Client</c> header in a minute, every method counted.</summary>
    private sealed class OnePerClientPolicy : IRateLimiterPolicy<string>
    {
        public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected => null;

        public RateLimitPartition<string> GetPartition(HttpContext httpContext) =>
            RateLimitPartition.GetFixedWindowLimiter(httpContext.Request.Headers["X-Client"].ToString(),
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 1, Window = TimeSpan.FromMinutes(1) });
    }
}
