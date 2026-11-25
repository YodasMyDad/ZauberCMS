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

        Assert.Equal(StatusCodes.Status401Unauthorized, status);
        Assert.False(passedOn);
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

    private async Task<(int Status, bool PassedOn)> RequestAsync(string path, bool signedIn)
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
            context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "member")], "Test"));
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
