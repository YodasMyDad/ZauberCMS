using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ZauberCMS.Core.Media.Middleware;
using ZauberCMS.Core.Settings;
using ZauberCMS.Core.Tests.Infrastructure;
using MediaItem = ZauberCMS.Core.Media.Models.Media;

namespace ZauberCMS.Core.Tests.Media;

/// <summary>
/// Media marked "requires authentication" must stay protected for as long as the file can be served: whatever
/// the case of the requested path, and also while the item sits in the recycle bin (ZA-053).
/// </summary>
public class RestrictedMediaTests : IAsyncLifetime
{
    /// <summary>Overridden by the PostgreSQL copy of this class at the end of the file.</summary>
    protected virtual string DatabaseProvider => TestSite.SqliteProvider;

    private TestSite _site = null!;

    public async ValueTask InitializeAsync() => _site = await TestSite.CreateAsync(databaseProvider: DatabaseProvider);

    public async ValueTask DisposeAsync() => await _site.DisposeAsync();

    [Theory]
    [InlineData("/media/members/Report.pdf")]
    [InlineData("/MEDIA/MEMBERS/REPORT.PDF")]
    [InlineData("/media/members/report.pdf")]
    public async Task An_anonymous_request_for_restricted_media_is_refused_whatever_the_case(string requestPath)
    {
        await AddMediaAsync("/media/members/Report.pdf", requiresAuthentication: true, deleted: false);

        var (status, passedOn) = await RequestAsync(requestPath, signedIn: false);

        Assert.Equal(StatusCodes.Status401Unauthorized, status);
        Assert.False(passedOn);
    }

    [Fact]
    public async Task Restricted_media_in_the_recycle_bin_is_still_refused()
    {
        await AddMediaAsync("/media/members/Report.pdf", requiresAuthentication: true, deleted: true);

        var (status, passedOn) = await RequestAsync("/media/members/Report.pdf", signedIn: false);

        Assert.Equal(StatusCodes.Status404NotFound, status);
        Assert.False(passedOn);
    }

    // Roadmap Phase 8: recycled media is not served, whether or not it was restricted
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Public_media_in_the_recycle_bin_is_not_found_by_anyone_but_an_administrator(bool signedIn)
    {
        await AddMediaAsync("/media/public/Logo.png", requiresAuthentication: false, deleted: true);

        var (status, passedOn) = await RequestAsync("/MEDIA/public/logo.png", signedIn);

        Assert.Equal(StatusCodes.Status404NotFound, status);
        Assert.False(passedOn);
    }

    // The static file handler reads "//" as "/", so the lookup must too (found by the Phase 8 audit)
    [Theory]
    [InlineData("/media//public/Logo.png", false)]
    [InlineData("//media/public//Logo.png", false)]
    [InlineData("/media//members/Report.pdf", true)]
    public async Task A_doubled_slash_does_not_get_round_the_check(string requestPath, bool restricted)
    {
        await AddMediaAsync("/media/public/Logo.png", requiresAuthentication: false, deleted: true);
        await AddMediaAsync("/media/members/Report.pdf", requiresAuthentication: true, deleted: false);

        var (status, passedOn) = await RequestAsync(requestPath, signedIn: false);

        Assert.Equal(restricted ? StatusCodes.Status401Unauthorized : StatusCodes.Status404NotFound, status);
        Assert.False(passedOn);
    }

    [Fact]
    public async Task An_administrator_gets_media_in_the_recycle_bin()
    {
        await AddMediaAsync("/media/public/Logo.png", requiresAuthentication: false, deleted: true);

        var (_, passedOn) = await RequestAsync("/media/public/Logo.png", signedIn: true, admin: true);

        Assert.True(passedOn);
    }

    [Theory]
    [InlineData(Constants.Roles.EditorRoleName, true)]
    [InlineData(Constants.Roles.StandardRoleName, false)]
    public async Task D67_whoever_may_open_the_Media_section_gets_media_in_the_recycle_bin(string role, bool expected)
    {
        // The seeded Editor role has the Media section; a front-end member has no section
        await AddMediaAsync("/media/public/Logo.png", requiresAuthentication: false, deleted: true);

        var (status, passedOn) = await RequestAsync("/media/public/Logo.png", signedIn: true, role: role);

        Assert.Equal(expected, passedOn);
        if (!expected)
        {
            Assert.Equal(StatusCodes.Status404NotFound, status);
        }
    }

    [Fact]
    public async Task A_recycled_item_does_not_hide_a_file_that_a_live_public_item_also_uses()
    {
        await AddMediaAsync("/media/public/Logo.png", requiresAuthentication: false, deleted: true);
        await AddMediaAsync("/media/public/Logo.png", requiresAuthentication: false, deleted: false);

        var (_, passedOn) = await RequestAsync("/media/public/Logo.png", signedIn: false);

        Assert.True(passedOn);
    }

    [Fact]
    public async Task A_signed_in_user_gets_restricted_media()
    {
        await AddMediaAsync("/media/members/Report.pdf", requiresAuthentication: true, deleted: false);

        var (_, passedOn) = await RequestAsync("/MEDIA/members/report.PDF", signedIn: true);

        Assert.True(passedOn);
    }

    [Fact]
    public async Task Public_media_is_passed_on_for_anonymous_requests()
    {
        await AddMediaAsync("/media/public/Logo.png", requiresAuthentication: false, deleted: false);

        var (_, passedOn) = await RequestAsync("/media/public/Logo.png", signedIn: false);

        Assert.True(passedOn);
    }

    private async Task AddMediaAsync(string url, bool requiresAuthentication, bool deleted)
    {
        await _site.QueryDatabaseAsync(async db =>
        {
            db.Medias.Add(new MediaItem
            {
                Name = "Report", Url = url, RequiresAuthentication = requiresAuthentication, Deleted = deleted
            });
            return await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
    }

    private async Task<(int Status, bool PassedOn)> RequestAsync(string path, bool signedIn, bool admin = false,
        string? role = null)
    {
        var passedOn = false;
        var middleware = new RestrictedMediaMiddleware(
            _ =>
            {
                passedOn = true;
                return Task.CompletedTask;
            },
            _site.Services,
            _site.Services.GetRequiredService<IOptions<ZauberSettings>>());

        var context = new DefaultHttpContext { RequestServices = _site.Services };
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        if (signedIn)
        {
            List<Claim> claims = [new Claim(ClaimTypes.Name, admin ? "admin" : "member")];
            if (admin)
            {
                claims.Add(new Claim(ClaimTypes.Role, Constants.Roles.AdminRoleName));
            }

            if (role != null)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
        }

        await middleware.InvokeAsync(context);
        return (context.Response.StatusCode, passedOn);
    }
}

/// <summary>The same tests on a real PostgreSQL (Roadmap D15); skipped without Docker.</summary>
public class RestrictedMediaTestsOnPostgreSql : RestrictedMediaTests
{
    protected override string DatabaseProvider => TestSite.PostgreSqlProvider;
}

/// <summary>The same tests on SQL Server; skipped without <c>ZAUBER_TEST_SQLSERVER</c>.</summary>
public class RestrictedMediaTestsOnSqlServer : RestrictedMediaTests
{
    protected override string DatabaseProvider => TestSite.SqlServerProvider;
}
