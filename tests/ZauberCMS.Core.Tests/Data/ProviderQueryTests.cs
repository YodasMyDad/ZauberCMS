using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Content.Interfaces;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Content.Parameters;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Membership.Interfaces;
using ZauberCMS.Core.Membership.Parameters;
using ZauberCMS.Core.Seo.Interfaces;
using ZauberCMS.Core.Seo.Models;
using ZauberCMS.Core.Seo.Parameters;
using ZauberCMS.Core.Tests.Infrastructure;

namespace ZauberCMS.Core.Tests.Data;

/// <summary>
/// Queries whose SQL depends on the provider: raw SQL (ZA-007) and case-insensitive search (ZA-017).
/// Runs on SQLite here and on PostgreSQL in <see cref="ProviderQueryTestsOnPostgreSql"/>.
/// </summary>
public class ProviderQueryTests : IAsyncLifetime
{
    protected virtual string DatabaseProvider => TestSite.SqliteProvider;

    private TestSite _site = null!;

    public async ValueTask InitializeAsync() =>
        _site = await TestSite.CreateAsync(databaseProvider: DatabaseProvider);

    public async ValueTask DisposeAsync() => await _site.DisposeAsync();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ZA189_the_redirect_middlewares_query_for_every_redirect_runs_on_the_provider()
    {
        await _site.QueryDatabaseAsync(async db =>
        {
            db.Redirects.AddRange(
                new SeoRedirect { FromUrl = "/a", ToUrl = "/b" },
                new SeoRedirect { FromUrl = "/c", ToUrl = "/d" },
                new SeoRedirect { FromUrl = "/e", ToUrl = "/f" });
            return await db.SaveChangesAsync(Ct);
        });

        using var scope = _site.CreateScope();
        // What RedirectMiddleware asks for: no cap (ZA-189)
        var all = await scope.ServiceProvider.GetRequiredService<ISeoService>().QueryRedirectsAsync(
            new QueryRedirectsParameters { Cached = false, Amount = int.MaxValue }, Ct);

        Assert.Equal(3, all.Count);
    }

    [Fact]
    public async Task ZA007_WherePathLike_finds_the_page_and_everything_below_it()
    {
        var pageType = await _site.CreateContentTypeAsync("page");
        var parent = await _site.CreateContentAsync(pageType, "Parent");
        var child = await _site.CreateContentAsync(pageType, "Child", parent.Id);
        var grandchild = await _site.CreateContentAsync(pageType, "Grandchild", child.Id);
        await _site.CreateContentAsync(pageType, "Elsewhere");

        var found = await _site.QueryDatabaseAsync(db =>
            db.Contents.WherePathLike(parent.Id).Select(c => c.Id).ToListAsync(Ct));

        Assert.Equal(new[] { parent.Id, child.Id, grandchild.Id }.Order(), found.Order());
    }

    [Fact]
    public async Task ZA007_WhereHasCompositionsUsing_finds_the_types_that_use_a_composition()
    {
        var composition = await _site.CreateContentTypeAsync("seo");
        var user = await _site.CreateContentTypeAsync("article");
        await _site.CreateContentTypeAsync("other");
        await _site.QueryDatabaseAsync(async db =>
        {
            var stored = await db.ContentTypes.SingleAsync(t => t.Id == user.Id, Ct);
            stored.CompositionIds = [composition.Id];
            return await db.SaveChangesAsync(Ct);
        });

        var found = await _site.QueryDatabaseAsync(db =>
            db.ContentTypes.WhereHasCompositionsUsing(composition.Id).Select(t => t.Id).ToListAsync(Ct));

        Assert.Equal([user.Id], found);
    }

    [Fact]
    public async Task ZA050_A_page_cannot_have_two_current_published_versions()
    {
        var pageType = await _site.CreateContentTypeAsync("page");
        var page = await _site.CreateContentAsync(pageType, "Home");
        Assert.Equal(1, await _site.QueryDatabaseAsync(db =>
            db.ContentVersions.CountAsync(v => v.ContentId == page.Id && v.IsCurrentPublished, Ct)));

        // The unique index existed on no provider: three unnamed ContentId indexes had collapsed into one
        await Assert.ThrowsAsync<DbUpdateException>(() => _site.QueryDatabaseAsync(db =>
        {
            db.ContentVersions.Add(new ContentVersion
            {
                ContentId = page.Id,
                VersionNumber = 99,
                Status = ContentVersionStatus.Published,
                IsCurrentPublished = true,
                DateCreated = DateTime.UtcNow
            });
            return db.SaveChangesAsync(Ct);
        }));
    }

    [Fact]
    public async Task ZA050_New_published_and_draft_versions_take_the_flags_over()
    {
        var pageType = await _site.CreateContentTypeAsync("page");
        var page = await _site.CreateContentAsync(pageType, "Home");

        using (var scope = _site.CreateScope())
        {
            var versioning = scope.ServiceProvider.GetRequiredService<IContentVersioningService>();
            foreach (var status in new[] { ContentVersionStatus.Published, ContentVersionStatus.Draft,
                         ContentVersionStatus.Published, ContentVersionStatus.Draft })
            {
                var result = await versioning.CreateVersionAsync(
                    new CreateContentVersionParameters { Content = page, Status = status }, Ct);
                Assert.True(result.Success, TestSite.Describe(result.Messages));
            }
        }

        var versions = await _site.QueryDatabaseAsync(db =>
            db.ContentVersions.Where(v => v.ContentId == page.Id).OrderBy(v => v.VersionNumber).ToListAsync(Ct));
        Assert.Equal(5, versions.Count);
        Assert.Equal(4, Assert.Single(versions, v => v.IsCurrentPublished).VersionNumber);
        Assert.Equal(5, Assert.Single(versions, v => v.IsLatestDraft).VersionNumber);
    }

    [Fact]
    public async Task ZA050_Upgrading_a_site_with_duplicate_published_flags_keeps_the_one_published_last()
    {
        var pageType = await _site.CreateContentTypeAsync("page");
        var page = await _site.CreateContentAsync(pageType, "Home");

        // Back to the schema before the index, where duplicates could be stored, and store one
        using (var scope = _site.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Core.Data.IZauberDbContext>();
            await db.Database.GetService<IMigrator>().MigrateAsync("AddUserDeletedFlag", Ct);
            var first = await db.ContentVersions.SingleAsync(v => v.ContentId == page.Id, Ct);
            // Version 2 was published, then version 1 restored (published again, later): 1 is live
            first.DatePublished = DateTime.UtcNow;
            db.ContentVersions.Add(new ContentVersion
            {
                ContentId = page.Id,
                VersionNumber = 2,
                Status = ContentVersionStatus.Published,
                IsCurrentPublished = true,
                DateCreated = DateTime.UtcNow.AddHours(-1),
                DatePublished = DateTime.UtcNow.AddHours(-1)
            });
            // And a version 3 that never had a publish date loses to both
            db.ContentVersions.Add(new ContentVersion
            {
                ContentId = page.Id,
                VersionNumber = 3,
                Status = ContentVersionStatus.Published,
                IsCurrentPublished = true,
                DateCreated = DateTime.UtcNow
            });
            await db.SaveChangesAsync(Ct);

            await db.Database.MigrateAsync(Ct);
        }

        var flagged = await _site.QueryDatabaseAsync(db => db.ContentVersions
            .Where(v => v.ContentId == page.Id && v.IsCurrentPublished).Select(v => v.VersionNumber).ToListAsync(Ct));
        Assert.Equal([1], flagged);
    }

    [Fact]
    public async Task ZA017_Search_ignores_case_also_when_the_server_runs_in_Turkish()
    {
        var pageType = await _site.CreateContentTypeAsync("Title page");
        await _site.CreateContentAsync(pageType, "Title page");
        await _site.CreateUserAsync("ILKER");

        var culture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
        try
        {
            // In Turkish "TITLE".ToLower() is "tıtle" (dotless ı), which matched nothing
            using var scope = _site.CreateScope();
            var contentService = scope.ServiceProvider.GetRequiredService<IContentService>();
            var membershipService = scope.ServiceProvider.GetRequiredService<IMembershipService>();

            var content = await contentService.QueryContentAsync(
                new QueryContentParameters { SearchTerm = "TITLE", Cached = false }, Ct);
            var types = await contentService.QueryContentTypesAsync(
                new QueryContentTypesParameters { SearchTerm = "TITLE" }, Ct);
            var users = await membershipService.QueryUsersAsync(
                new QueryUsersParameters { SearchTerm = "ILKER", Cached = false }, Ct);

            Assert.Single(content.Items);
            Assert.Single(types.Items);
            Assert.Single(users.Items);
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }
}

/// <summary>The same tests on a real PostgreSQL (Roadmap D15); skipped without Docker.</summary>
public class ProviderQueryTestsOnPostgreSql : ProviderQueryTests
{
    protected override string DatabaseProvider => TestSite.PostgreSqlProvider;
}

/// <summary>The same tests on SQL Server; skipped without <c>ZAUBER_TEST_SQLSERVER</c>.</summary>
public class ProviderQueryTestsOnSqlServer : ProviderQueryTests
{
    protected override string DatabaseProvider => TestSite.SqlServerProvider;
}
