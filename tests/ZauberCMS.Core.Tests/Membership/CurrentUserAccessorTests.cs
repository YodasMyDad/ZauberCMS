using System.Security.Claims;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Content.Parameters;
using ZauberCMS.Core.Content.Services;
using ZauberCMS.Core.Media.Models;
using ZauberCMS.Core.Media.Parameters;
using ZauberCMS.Core.Media.Services;
using ZauberCMS.Core.Membership.Interfaces;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Membership.Services;
using ZauberCMS.Core.Tests.Infrastructure;
using ContentItem = ZauberCMS.Core.Content.Models.Content;
using MediaItem = ZauberCMS.Core.Media.Models.Media;

namespace ZauberCMS.Core.Tests.Membership;

/// <summary>
/// ZA-093 (Roadmap Phase 10a): the services find their caller through <see cref="ICurrentUserAccessor"/>, which works
/// outside a Blazor render (a controller, a job), and a write with nobody signed in is saved, recording no user,
/// instead of throwing. Services trust their caller (D27): they record who made a change, they don't refuse one.
/// </summary>
public class CurrentUserAccessorTests : IAsyncLifetime
{
    private TestSite _site = null!;
    private ContentType _pageType = null!;

    public async ValueTask InitializeAsync()
    {
        _site = await TestSite.CreateAsync();
        _pageType = await _site.CreateContentTypeAsync("page");
    }

    public async ValueTask DisposeAsync() => await _site.DisposeAsync();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>The accessor as a hosted service or controller gets it: the real provider was never given a state
    /// (it throws when asked), with or without a request.</summary>
    private CurrentUserAccessor OutsideARender(HttpContext? request = null) => new(
        new ServerAuthenticationStateProvider(),
        new HttpContextAccessor { HttpContext = request },
        _site.Services.GetRequiredService<IServiceScopeFactory>());

    private static ClaimsPrincipal PrincipalFor(User user) => new(new ClaimsIdentity(
    [
        new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
        new Claim(ClaimTypes.Name, user.UserName ?? string.Empty)
    ], "Test"));

    [Fact]
    public async Task ZA093_Outside_a_render_the_caller_is_the_requests_user()
    {
        var accessor = OutsideARender(new DefaultHttpContext { User = PrincipalFor(_site.CurrentUser) });

        var user = await accessor.GetUserAsync(Ct);

        Assert.Equal(_site.CurrentUser.Id, user?.Id);
    }

    [Fact]
    public async Task ZA093_With_no_render_and_no_request_the_caller_is_nobody_instead_of_an_exception()
    {
        var accessor = OutsideARender();

        var principal = await accessor.GetPrincipalAsync(Ct);

        Assert.False(principal.Identity?.IsAuthenticated);
        Assert.Null(await accessor.GetUserAsync(Ct));
    }

    [Fact]
    public async Task ZA093_RunAs_wins_over_the_signed_in_user_and_is_undone_in_turn()
    {
        var other = await _site.CreateUserAsync("runner");
        var third = await _site.CreateUserAsync("nested");
        using var scope = _site.CreateScope();
        var accessor = scope.ServiceProvider.GetRequiredService<ICurrentUserAccessor>();

        using (accessor.RunAs(PrincipalFor(other)))
        {
            Assert.Equal(other.Id, (await accessor.GetUserAsync(Ct))?.Id);
            using (accessor.RunAs(PrincipalFor(third)))
            {
                Assert.Equal(third.Id, (await accessor.GetUserAsync(Ct))?.Id);
            }

            Assert.Equal(other.Id, (await accessor.GetUserAsync(Ct))?.Id);
        }

        Assert.Equal(_site.CurrentUser.Id, (await accessor.GetUserAsync(Ct))?.Id);
    }

    [Fact]
    public async Task ZA093_RunAs_applies_to_its_own_flow_not_to_others_in_the_same_scope()
    {
        // A circuit is one scope for every event its user raises: a RunAs in one handler must not leak into another
        var other = await _site.CreateUserAsync("runner");
        using var scope = _site.CreateScope();
        var accessor = scope.ServiceProvider.GetRequiredService<ICurrentUserAccessor>();
        var go = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var otherHandler = Task.Run(async () =>
        {
            await go.Task;
            return await accessor.GetUserAsync(Ct);
        }, Ct);

        using (accessor.RunAs(PrincipalFor(other)))
        {
            go.SetResult();
            Assert.Equal(_site.CurrentUser.Id, (await otherHandler)?.Id);
        }
    }

    [Fact]
    public async Task ZA093_RunAs_does_not_replace_the_user_of_another_scope_that_has_its_own()
    {
        // An AppState handler in another circuit runs on the publisher's flow: it must keep its circuit's user
        var other = await _site.CreateUserAsync("runner");
        using var publisherScope = _site.CreateScope();
        using var otherCircuit = _site.CreateScope();
        var publisher = publisherScope.ServiceProvider.GetRequiredService<ICurrentUserAccessor>();
        var elsewhere = otherCircuit.ServiceProvider.GetRequiredService<ICurrentUserAccessor>();

        using (publisher.RunAs(PrincipalFor(other)))
        {
            Assert.Equal(other.Id, (await publisher.GetUserAsync(Ct))?.Id);
            Assert.Equal(_site.CurrentUser.Id, (await elsewhere.GetUserAsync(Ct))?.Id);
            // A scope with no render of its own (a job's) does run as the flow's user
            Assert.Equal(other.Id, (await OutsideARender().GetUserAsync(Ct))?.Id);
        }
    }

    [Fact]
    public async Task ZA093_A_request_signed_in_by_another_scheme_counts_as_nobody()
    {
        // A bearer token's subject is not one of our user ids; the store would throw on it
        var accessor = OutsideARender(new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, "auth0|5f7c8ec7c33c6c004bbafe82")], "Bearer"))
        });

        Assert.Null(await accessor.GetUserAsync(Ct));
    }

    [Fact]
    public async Task ZA093_A_job_with_nobody_signed_in_saves_content_and_records_no_user()
    {
        using var scope = _site.CreateScope();
        // Built as a hosted service's scope would build it: no render, no request
        var contentService = ActivatorUtilities.CreateInstance<ContentService>(scope.ServiceProvider, OutsideARender());
        var auditsBefore = await _site.QueryDatabaseAsync(db => db.Audits.CountAsync(Ct));

        var saved = await contentService.SaveContentAsync(new SaveContentParameters
        {
            Content = new ContentItem
            {
                Name = "Imported",
                ContentTypeId = _pageType.Id,
                ContentTypeAlias = _pageType.Alias,
                IsRootContent = true,
                Published = true
            }
        }, Ct);
        Assert.True(saved.Success, TestSite.Describe(saved.Messages));

        // A save of where a page is shown (sort, move) takes the other path
        var moved = saved.Entity!;
        moved.SortOrder = 7;
        var sorted = await contentService.SaveContentAsync(new SaveContentParameters
        {
            Content = moved,
            ExcludePropertyData = true
        }, Ct);
        Assert.True(sorted.Success, TestSite.Describe(sorted.Messages));

        var type = await contentService.SaveContentTypeAsync(new SaveContentTypeParameters
        {
            ContentType = new ContentType { Name = "Imported type", Alias = "importedType", AllowAtRoot = true }
        }, Ct);
        Assert.True(type.Success, TestSite.Describe(type.Messages));

        var stored = await _site.QueryDatabaseAsync(db =>
            db.Contents.AsNoTracking().SingleAsync(x => x.Id == saved.Entity!.Id, Ct));
        Assert.Null(stored.LastUpdatedById);
        Assert.Equal(7, stored.SortOrder);
        var version = await _site.QueryDatabaseAsync(db =>
            db.ContentVersions.AsNoTracking().FirstAsync(x => x.ContentId == stored.Id, Ct));
        Assert.Null(version.CreatedById);
        Assert.Null(await _site.QueryDatabaseAsync(db =>
            db.ContentTypes.AsNoTracking().Where(x => x.Id == type.Entity!.Id).Select(x => x.LastUpdatedById)
                .SingleAsync(Ct)));
        // Nobody to name: no audit line, as for the other services
        Assert.Equal(auditsBefore, await _site.QueryDatabaseAsync(db => db.Audits.CountAsync(Ct)));
    }

    [Fact]
    public async Task ZA093_A_job_with_nobody_signed_in_saves_media()
    {
        using var scope = _site.CreateScope();
        var mediaService = ActivatorUtilities.CreateInstance<MediaService>(scope.ServiceProvider, OutsideARender());

        var saved = await mediaService.SaveMediaAsync(new SaveMediaParameters
        {
            MediaToSave = new MediaItem { Name = "Imports", MediaType = MediaType.Folder }
        }, Ct);

        Assert.True(saved.Success, TestSite.Describe(saved.Messages));
        Assert.Null(await _site.QueryDatabaseAsync(db =>
            db.Medias.AsNoTracking().Where(x => x.Id == saved.Entity!.Id).Select(x => x.LastUpdatedById)
                .SingleAsync(Ct)));
    }

    [Fact]
    public async Task ZA093_A_job_that_runs_as_a_user_is_recorded_as_that_user()
    {
        var importer = await _site.CreateUserAsync("importer");
        var accessor = OutsideARender();
        using var scope = _site.CreateScope();
        var contentService = ActivatorUtilities.CreateInstance<ContentService>(scope.ServiceProvider, accessor);

        using (accessor.RunAs(PrincipalFor(importer)))
        {
            var saved = await contentService.SaveContentAsync(new SaveContentParameters
            {
                Content = new ContentItem
                {
                    Name = "Imported",
                    ContentTypeId = _pageType.Id,
                    ContentTypeAlias = _pageType.Alias,
                    IsRootContent = true,
                    Published = true
                }
            }, Ct);
            Assert.True(saved.Success, TestSite.Describe(saved.Messages));

            var stored = await _site.QueryDatabaseAsync(db =>
                db.Contents.AsNoTracking().SingleAsync(x => x.Id == saved.Entity!.Id, Ct));
            Assert.Equal(importer.Id, stored.LastUpdatedById);
        }
    }
}
