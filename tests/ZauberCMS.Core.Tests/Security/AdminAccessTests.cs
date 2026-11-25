using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ZauberCMS.Core.Data.Interfaces;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Membership.Middleware;
using ZauberCMS.Core.Seo;
using ZauberCMS.Core.Seo.Interfaces;
using ZauberCMS.Core.Seo.Models;
using ZauberCMS.Core.Seo.Parameters;
using ZauberCMS.Core.Settings;
using ZauberCMS.Core.Tests.Infrastructure;

namespace ZauberCMS.Core.Tests.Security;

/// <summary>
/// Everything under /admin is for administrators unless a page says otherwise itself, and the admin IP allow-list
/// applies to every admin request (ZA-132, ZA-060).
/// </summary>
public class AdminAccessTests : IAsyncLifetime
{
    private TestSite _site = null!;

    public async ValueTask InitializeAsync() => _site = await TestSite.CreateAsync();

    public async ValueTask DisposeAsync() => await _site.DisposeAsync();

    [Theory]
    [InlineData("/admin", true)]
    [InlineData("/Admin/Content", true)]
    [InlineData("/ADMIN/seo/redirects?x=1", true)]
    [InlineData("https://site.test/admin/users#top", true)]
    [InlineData("/administrator", false)]
    [InlineData("/blog/admin", false)]
    [InlineData("/", false)]
    [InlineData("", false)]
    public void Admin_paths_are_recognised_whatever_the_case(string path, bool expected)
    {
        Assert.Equal(expected, AdminAccess.IsAdminPath(path));
    }

    [Fact]
    public async Task An_admin_page_without_its_own_authorisation_sends_an_anonymous_visitor_to_sign_in()
    {
        var (context, passedOn) = await RequestAsync("/admin/plugin-page", user: null);

        Assert.False(passedOn);
        Assert.Equal(StatusCodes.Status302Found, context.Response.StatusCode);
        Assert.Contains("/account/login", context.Response.Headers.Location.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task An_admin_page_without_its_own_authorisation_refuses_a_signed_in_non_administrator()
    {
        var (context, passedOn) = await RequestAsync("/admin/plugin-page", User("Member"));

        Assert.False(passedOn);
        Assert.NotEqual(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Fact]
    public async Task An_administrator_gets_through()
    {
        var (_, passedOn) = await RequestAsync("/admin/plugin-page", User(Constants.Roles.AdminRoleName));

        Assert.True(passedOn);
    }

    [Fact]
    public async Task A_page_that_declares_its_own_authorisation_is_left_to_the_authorisation_middleware()
    {
        var (_, withAuthorize) = await RequestAsync("/admin/editors-only", user: null, new AuthorizeAttribute { Roles = "Editor" });
        var (_, withAllowAnonymous) = await RequestAsync("/admin/public-status", user: null, new AllowAnonymousAttribute());

        Assert.True(withAuthorize);
        Assert.True(withAllowAnonymous);
    }

    [Fact]
    public async Task Pages_outside_admin_are_not_touched()
    {
        var (_, passedOn) = await RequestAsync("/blog/post", user: null);

        Assert.True(passedOn);
    }

    [Fact]
    public async Task With_an_IP_allow_list_an_admin_request_from_another_address_gets_403_even_for_an_administrator()
    {
        using (var scope = _site.CreateScope())
        {
            var dataService = scope.ServiceProvider.GetRequiredService<IDataService>();
            Assert.True(await dataService.SaveGlobalSettings(new GlobalSettings { AllowedAdminIpAddress = ["203.0.113.5"] }));
        }

        var admin = User(Constants.Roles.AdminRoleName);
        var (refused, refusedPassedOn) = await RequestAsync("/admin/content", admin, remoteIp: "198.51.100.7");
        var (_, allowedPassedOn) = await RequestAsync("/admin/content", admin, remoteIp: "203.0.113.5");
        var (_, publicPassedOn) = await RequestAsync("/blog/post", user: null, remoteIp: "198.51.100.7");

        Assert.False(refusedPassedOn);
        Assert.Equal(StatusCodes.Status403Forbidden, refused.Response.StatusCode);
        Assert.True(allowedPassedOn);
        Assert.True(publicPassedOn);
    }

    [Theory]
    [InlineData("203.0.113.5", "203.0.113.5", true)]
    [InlineData("::ffff:203.0.113.5", "203.0.113.5", true)]
    [InlineData("203.0.113.6", "203.0.113.5", false)]
    [InlineData("203.0.113.5", " 203.0.113.5 ", true)]
    [InlineData("203.0.113.5", "not-an-address", false)]
    public void The_allow_list_compares_addresses_not_text(string remote, string allowed, bool expected)
    {
        Assert.Equal(expected, AdminAccess.IsAllowedAddress(IPAddress.Parse(remote), [allowed]));
    }

    [Fact]
    public void An_unknown_remote_address_is_refused_when_a_list_is_set_and_allowed_when_it_is_empty()
    {
        Assert.False(AdminAccess.IsAllowedAddress(null, ["203.0.113.5"]));
        Assert.True(AdminAccess.IsAllowedAddress(null, []));
        Assert.True(AdminAccess.IsAllowedAddress(IPAddress.Parse("198.51.100.7"), ["", " "]));
    }

    [Fact]
    public void Forwarded_headers_trust_only_the_configured_proxies()
    {
        var options = ZauberSetup.BuildForwardedHeadersOptions(new ForwardedHeadersSettings
        {
            Enabled = true, KnownProxies = ["10.0.0.4"], KnownNetworks = ["10.1.0.0/16"]
        });

        Assert.Equal(ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto, options.ForwardedHeaders);
        Assert.Contains(IPAddress.Parse("10.0.0.4"), options.KnownProxies);
        Assert.Contains(options.KnownIPNetworks, n => n.Contains(IPAddress.Parse("10.1.2.3")));
        Assert.DoesNotContain(options.KnownIPNetworks, n => n.Contains(IPAddress.Parse("198.51.100.7")));
    }

    [Theory]
    [InlineData("proxy.internal", null)]
    [InlineData(null, "10.0.0.0")]
    public void A_proxy_setting_that_is_not_an_address_stops_startup(string? proxy, string? network)
    {
        Assert.Throws<InvalidOperationException>(() => ZauberSetup.BuildForwardedHeadersOptions(
            new ForwardedHeadersSettings
            {
                Enabled = true,
                KnownProxies = proxy == null ? null : [proxy],
                KnownNetworks = network == null ? null : [network]
            }));
    }

    [Theory]
    [InlineData("/new-page", true)]
    [InlineData("/", true)]
    [InlineData("https://partner.example/landing", true)]
    [InlineData("http://partner.example", true)]
    [InlineData("//evil.example", false)]
    [InlineData("/\\evil.example", false)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("data:text/html,<script>alert(1)</script>", false)]
    [InlineData("new-page", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void A_redirect_may_go_to_a_local_path_or_an_http_address_only(string? toUrl, bool expected)
    {
        Assert.Equal(expected, RedirectTarget.IsValid(toUrl));
    }

    [Fact]
    public async Task Saving_a_redirect_to_a_script_address_is_refused()
    {
        using var scope = _site.CreateScope();
        var seoService = scope.ServiceProvider.GetRequiredService<ISeoService>();

        var refused = await seoService.SaveRedirectAsync(new SaveRedirectParameters
        {
            Redirect = new SeoRedirect { FromUrl = "/old", ToUrl = "javascript:alert(1)" }
        }, TestContext.Current.CancellationToken);
        var saved = await seoService.SaveRedirectAsync(new SaveRedirectParameters
        {
            Redirect = new SeoRedirect { FromUrl = "/old", ToUrl = "/new" }
        }, TestContext.Current.CancellationToken);

        Assert.False(refused.Success);
        Assert.True(saved.Success, TestSite.Describe(saved.Messages));
    }

    private static ClaimsPrincipal User(string role) => new(new ClaimsIdentity(
        [new Claim(ClaimTypes.Name, "someone"), new Claim(ClaimTypes.Role, role)], "Test"));

    private async Task<(HttpContext Context, bool PassedOn)> RequestAsync(string path, ClaimsPrincipal? user,
        object? endpointMetadata = null, string? remoteIp = null)
    {
        using var scope = _site.CreateScope();
        var passedOn = false;
        var middleware = new AdminAccessMiddleware(_ =>
        {
            passedOn = true;
            return Task.CompletedTask;
        }, NullLogger<AdminAccessMiddleware>.Instance);

        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        context.Connection.RemoteIpAddress = remoteIp == null ? null : IPAddress.Parse(remoteIp);
        if (user != null)
        {
            context.User = user;
        }

        var metadata = endpointMetadata == null ? new EndpointMetadataCollection() : new EndpointMetadataCollection(endpointMetadata);
        context.SetEndpoint(new Endpoint(_ => Task.CompletedTask, metadata, "test page"));

        await middleware.InvokeAsync(context, scope.ServiceProvider.GetRequiredService<IDataService>());
        return (context, passedOn);
    }
}
