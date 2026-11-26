using System.Text;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZauberCMS.Core.Installer;
using ZauberCMS.Core.Installer.Middleware;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Tests.Infrastructure;

namespace ZauberCMS.Core.Tests.Installer;

/// <summary>
/// Roadmap D20/D21: while a site is not installed every request goes to the installer; once it is, the installer
/// is gone and its address is an ordinary one again.
/// </summary>
public class InstallerMiddlewareTests : IAsyncLifetime
{
    private TestSite _site = null!;
    private InstallerMiddleware _middleware = null!;
    private bool _nextCalled;

    public async ValueTask InitializeAsync()
    {
        _site = await TestSite.CreateAsync(createUser: false);
        // What start-up does for a site that has a database
        _site.Services.GetRequiredService<InstallState>().MarkDatabaseInitialised();
        _middleware = new InstallerMiddleware(_ =>
        {
            _nextCalled = true;
            return Task.CompletedTask;
        }, _site.Services.GetRequiredService<InstallState>(), _site.Services.GetRequiredService<ILoggerFactory>());
    }

    public async ValueTask DisposeAsync() => await _site.DisposeAsync();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<(HttpContext Context, string Body)> SendAsync(string method, string path,
        Dictionary<string, string>? form = null, string? cookie = null, string? address = "203.0.113.7")
    {
        using var scope = _site.CreateScope();
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        context.Request.Method = method;
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("site.test");
        context.Request.Path = path;
        context.Connection.RemoteIpAddress = address == null ? null : System.Net.IPAddress.Parse(address);
        context.Response.Body = new MemoryStream();
        if (cookie != null)
        {
            context.Request.Headers.Cookie = cookie;
        }

        if (form != null)
        {
            var encoded = await new FormUrlEncodedContent(form).ReadAsByteArrayAsync(Ct);
            context.Request.ContentType = "application/x-www-form-urlencoded";
            context.Request.ContentLength = encoded.Length;
            context.Request.Body = new MemoryStream(encoded);
        }

        // SignInManager reads the request from the accessor
        scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = context;
        _nextCalled = false;
        await _middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        return (context, await new StreamReader(context.Response.Body, Encoding.UTF8).ReadToEndAsync(Ct));
    }

    // The installer page with its antiforgery cookie and token, as a browser has them after a GET
    private async Task<(string Cookie, string Field, string Token)> OpenInstallerAsync()
    {
        var (context, body) = await SendAsync("GET", "/install");
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        var cookie = context.Response.Headers.SetCookie.ToString().Split(';')[0];
        var field = _site.Services.GetRequiredService<IOptions<AntiforgeryOptions>>().Value.FormFieldName;
        var marker = $"name=\"{field}\" value=\"";
        var start = body.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
        return (cookie, field, body[start..body.IndexOf('"', start)]);
    }

    private static Dictionary<string, string> SetupForm(string field, string token) => new()
    {
        [field] = token,
        ["step"] = "setup",
        ["adminName"] = "Ada Admin",
        ["adminEmail"] = "ada@example.test",
        ["adminPassword"] = "Passw0rd-For-Tests",
        ["siteName"] = "Wand & Hat",
        ["language"] = "en-GB",
        ["starterKit"] = ""
    };

    [Theory]
    [InlineData("/")]
    [InlineData("/admin")]
    [InlineData("/Account/Register")]
    [InlineData("/media/picture.jpg")]
    public async Task Before_the_install_every_page_leads_to_the_installer(string path)
    {
        var (context, _) = await SendAsync("GET", path);

        Assert.False(_nextCalled);
        Assert.Equal(StatusCodes.Status302Found, context.Response.StatusCode);
        Assert.Equal("/install", context.Response.Headers.Location.ToString());
    }

    [Fact]
    public async Task Before_the_install_a_post_to_the_site_is_refused()
    {
        var (context, _) = await SendAsync("POST", "/Account/Login", new Dictionary<string, string> { ["x"] = "y" });

        Assert.False(_nextCalled);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, context.Response.StatusCode);
    }

    [Fact]
    public async Task The_installer_page_is_locked_down_and_never_cached()
    {
        var (context, body) = await SendAsync("GET", "/install");

        Assert.False(_nextCalled);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Contains("Create the administrator", body);
        Assert.Contains("no-store", context.Response.Headers.CacheControl.ToString());
        Assert.Equal("DENY", context.Response.Headers.XFrameOptions.ToString());
        var policy = context.Response.Headers.ContentSecurityPolicy.ToString();
        Assert.Contains("default-src 'none'", policy);
        Assert.Contains("frame-ancestors 'none'", policy);
        // The only style and script allowed are the page's own, by a nonce that is new for each response
        var nonce = policy.Split("'nonce-")[1].Split('\'')[0];
        Assert.Contains($"<style nonce=\"{nonce}\">", body);
        Assert.Contains($"<script nonce=\"{nonce}\">", body);
        var (second, _) = await SendAsync("GET", "/install");
        Assert.DoesNotContain(nonce, second.Response.Headers.ContentSecurityPolicy.ToString());
    }

    [Fact]
    public async Task A_post_without_the_antiforgery_token_installs_nothing()
    {
        var (context, body) = await SendAsync("POST", "/install", SetupForm("__RequestVerificationToken", "forged"));

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.Contains("open for too long", body);
        Assert.False(await _site.QueryDatabaseAsync(db => db.Users.AnyAsync(Ct)));
    }

    [Fact]
    public async Task Posting_the_form_installs_the_site_signs_the_administrator_in_and_closes_the_installer()
    {
        var (cookie, field, token) = await OpenInstallerAsync();

        var (context, _) = await SendAsync("POST", "/install", SetupForm(field, token), cookie);

        Assert.Equal(StatusCodes.Status303SeeOther, context.Response.StatusCode);
        Assert.Equal("/admin", context.Response.Headers.Location.ToString());
        Assert.Contains(IdentityConstants.ApplicationScheme, context.Response.Headers.SetCookie.ToString());
        Assert.True(_site.Services.GetRequiredService<InstallState>().IsInstalled);
        using (var scope = _site.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var admin = await userManager.FindByEmailAsync("ada@example.test");
            Assert.NotNull(admin);
            Assert.True(await userManager.IsInRoleAsync(admin, Constants.Roles.AdminRoleName));
        }

        // From now on the middleware is out of the way, for its own address too
        var (page, _) = await SendAsync("GET", "/about");
        Assert.True(_nextCalled);
        Assert.Equal(StatusCodes.Status200OK, page.Response.StatusCode);
        await SendAsync("GET", "/install");
        Assert.True(_nextCalled);
        await SendAsync("POST", "/install", SetupForm(field, token), cookie);
        Assert.True(_nextCalled);
        Assert.Equal(1, await _site.QueryDatabaseAsync(db => db.Users.CountAsync(Ct)));
    }

    [Fact]
    public async Task A_refused_install_shows_the_form_again_with_what_was_typed_except_the_password()
    {
        var (cookie, field, token) = await OpenInstallerAsync();
        var form = SetupForm(field, token);
        form["adminPassword"] = "weak";

        var (context, body) = await SendAsync("POST", "/install", form, cookie);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.Contains("value=\"Ada Admin\"", body);
        Assert.Contains("value=\"Wand &amp; Hat\"", body);
        Assert.Contains("data-start=\"admin\"", body);
        Assert.DoesNotContain("value=\"weak\"", body);
        Assert.False(_site.Services.GetRequiredService<InstallState>().IsInstalled);
    }

    [Fact]
    public async Task A_site_another_server_installed_lets_requests_through()
    {
        // The flag appears in the database without this process having written it
        await _site.QueryDatabaseAsync(async db =>
        {
            db.GlobalDatas.Add(new ZauberCMS.Core.Data.Models.GlobalData
            {
                Alias = ZauberCMS.Core.Installer.Models.InstallFlag.Alias, Data = "{}"
            });
            return await db.SaveChangesAsync(Ct);
        });

        var (context, _) = await SendAsync("GET", "/install");

        Assert.False(_nextCalled);
        Assert.Equal(StatusCodes.Status303SeeOther, context.Response.StatusCode);
        Assert.Equal("/", context.Response.Headers.Location.ToString());
        await SendAsync("GET", "/");
        Assert.True(_nextCalled);
    }

    // A database that was configured after start-up: nothing has prepared it yet
    [Fact]
    public async Task The_first_request_prepares_a_database_that_start_up_did_not()
    {
        await using var site = await TestSite.CreateAsync(createUser: false);
        var state = site.Services.GetRequiredService<InstallState>();
        Assert.False(state.IsDatabaseInitialised);
        var middleware = new InstallerMiddleware(_ => Task.CompletedTask, state,
            site.Services.GetRequiredService<ILoggerFactory>());

        using var scope = site.CreateScope();
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        context.Request.Method = "GET";
        context.Request.Path = "/install";
        context.Response.Body = new MemoryStream();
        await middleware.InvokeAsync(context);

        Assert.True(state.IsDatabaseInitialised);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Fact]
    public async Task A_configured_database_that_cannot_be_opened_is_reported_on_the_installer_page()
    {
        var contentRoot = TestSite.CreateContentRoot();
        // A file in a folder that does not exist: the connection cannot be opened
        var missing = Path.Combine(contentRoot, "no-such-folder", "site.db");
        await using var app = TestSite.BuildApp(TestSite.SqliteProvider, $"DataSource={missing}", contentRoot,
            validateContainer: false);
        var state = app.Services.GetRequiredService<InstallState>();
        var called = false;
        var middleware = new InstallerMiddleware(_ =>
        {
            called = true;
            return Task.CompletedTask;
        }, state, app.Services.GetRequiredService<ILoggerFactory>());

        async Task<(DefaultHttpContext Context, string Body)> GetAsync(string path)
        {
            using var scope = app.Services.CreateScope();
            var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
            context.Request.Method = "GET";
            context.Request.Path = path;
            context.Response.Body = new MemoryStream();
            await middleware.InvokeAsync(context);
            context.Response.Body.Position = 0;
            return (context, await new StreamReader(context.Response.Body).ReadToEndAsync(Ct));
        }

        var (page, _) = await GetAsync("/");
        var (installer, body) = await GetAsync("/install");

        Assert.False(called);
        Assert.Equal(StatusCodes.Status302Found, page.Response.StatusCode);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, installer.Response.StatusCode);
        Assert.Contains("The database cannot be used", body);
        Assert.False(state.IsDatabaseInitialised);
        Directory.Delete(contentRoot, recursive: true);
    }

    [Fact]
    public async Task Posts_to_the_installer_are_limited_per_address()
    {
        var form = new Dictionary<string, string> { ["step"] = "setup" };
        var statuses = new List<int>();
        for (var i = 0; i < 22; i++)
        {
            statuses.Add((await SendAsync("POST", "/install", form, address: "198.51.100.9")).Context.Response.StatusCode);
        }

        Assert.Equal(20, statuses.Count(s => s == StatusCodes.Status400BadRequest));
        Assert.Equal(2, statuses.Count(s => s == StatusCodes.Status429TooManyRequests));
        // Somebody else is not affected
        var (other, _) = await SendAsync("POST", "/install", form, address: "198.51.100.10");
        Assert.Equal(StatusCodes.Status400BadRequest, other.Response.StatusCode);
    }

    [Fact]
    public async Task The_database_step_of_a_site_that_has_a_database_is_not_run()
    {
        var (cookie, field, token) = await OpenInstallerAsync();

        var (context, _) = await SendAsync("POST", "/install", new Dictionary<string, string>
        {
            [field] = token,
            ["step"] = "database",
            ["provider"] = "Sqlite",
            ["sqliteFile"] = Path.Combine(Path.GetTempPath(), $"zauber-must-not-exist-{Guid.NewGuid():N}.db"),
            ["action"] = "save"
        }, cookie);

        Assert.Equal(StatusCodes.Status303SeeOther, context.Response.StatusCode);
        Assert.Equal("/install", context.Response.Headers.Location.ToString());
    }
}
