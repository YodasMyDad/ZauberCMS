using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Content.Interfaces;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Content.Parameters;
using ZauberCMS.Core.Shared.Models;
using ZauberCMS.Core.Tests.Infrastructure;
using ContentItem = ZauberCMS.Core.Content.Models.Content;

namespace ZauberCMS.Core.Tests.Content;

/// <summary>
/// Soft delete has no global query filter (AGENTS.md rule 4): these pin the defaults every caller relies on.
/// ZA-001 was caused by one of these defaults changing without its callers being updated.
/// </summary>
public class ContentSoftDeleteTests : IAsyncLifetime
{
    /// <summary>Overridden by the PostgreSQL copy of this class at the end of the file.</summary>
    protected virtual string DatabaseProvider => TestSite.SqliteProvider;

    private TestSite _site = null!;
    private ContentType _pageType = null!;

    public async ValueTask InitializeAsync()
    {
        _site = await TestSite.CreateAsync(databaseProvider: DatabaseProvider);
        _pageType = await _site.CreateContentTypeAsync("page");
    }

    public async ValueTask DisposeAsync() => await _site.DisposeAsync();

    [Fact]
    public async Task Recycled_content_is_kept_in_the_database_and_flagged_deleted()
    {
        var page = await _site.CreateContentAsync(_pageType, "About");

        var result = await RecycleAsync(page.Id);

        Assert.True(result.Success, TestSite.Describe(result.Messages));
        var stored = await _site.QueryDatabaseAsync(db => db.Contents.AsNoTracking().SingleAsync(x => x.Id == page.Id));
        Assert.True(stored.Deleted);
    }

    [Fact]
    public async Task Get_does_not_return_recycled_content_by_default()
    {
        var page = await _site.CreateContentAsync(_pageType, "About");
        await RecycleAsync(page.Id);

        var found = await GetAsync(new GetContentParameters { Id = page.Id });

        Assert.Null(found);
    }

    // The recycle bin (restore, permanent delete, open) depends on this: ZA-001.
    [Fact]
    public async Task Get_returns_recycled_content_when_IncludeDeleted_is_set()
    {
        var page = await _site.CreateContentAsync(_pageType, "About");
        await RecycleAsync(page.Id);

        var found = await GetAsync(new GetContentParameters { Id = page.Id, IncludeDeleted = true });

        Assert.NotNull(found);
        Assert.True(found.Deleted);
    }

    [Fact]
    public async Task Query_excludes_recycled_content_by_default()
    {
        var kept = await _site.CreateContentAsync(_pageType, "Kept");
        var recycled = await _site.CreateContentAsync(_pageType, "Recycled");
        await RecycleAsync(recycled.Id);

        var result = await QueryAsync(new QueryContentParameters { AmountPerPage = 50 });

        Assert.Contains(result, x => x.Id == kept.Id);
        Assert.DoesNotContain(result, x => x.Id == recycled.Id);
    }

    [Fact]
    public async Task Query_with_IsDeleted_true_returns_only_recycled_content()
    {
        var kept = await _site.CreateContentAsync(_pageType, "Kept");
        var recycled = await _site.CreateContentAsync(_pageType, "Recycled");
        await RecycleAsync(recycled.Id);

        var result = await QueryAsync(new QueryContentParameters { AmountPerPage = 50, IsDeleted = true });

        Assert.Contains(result, x => x.Id == recycled.Id);
        Assert.DoesNotContain(result, x => x.Id == kept.Id);
    }

    // The service calls the recycle-bin "Restore" action makes, in order.
    [Fact]
    public async Task Recycled_content_can_be_restored()
    {
        var page = await _site.CreateContentAsync(_pageType, "About");
        await RecycleAsync(page.Id);

        var recycled = await GetAsync(new GetContentParameters { Id = page.Id, IncludeDeleted = true });
        Assert.NotNull(recycled);
        recycled.Deleted = false;
        using (var scope = _site.CreateScope())
        {
            var contentService = scope.ServiceProvider.GetRequiredService<IContentService>();
            var saved = await contentService.SaveContentAsync(new SaveContentParameters { Content = recycled },
                TestContext.Current.CancellationToken);
            Assert.True(saved.Success, TestSite.Describe(saved.Messages));
        }

        var restored = await GetAsync(new GetContentParameters { Id = page.Id });
        Assert.NotNull(restored);
        Assert.False(restored.Deleted);
    }

    [Fact]
    public async Task Permanent_delete_removes_the_row()
    {
        var page = await _site.CreateContentAsync(_pageType, "About");
        await RecycleAsync(page.Id);

        var result = await DeleteAsync(page.Id, moveToRecycleBin: false);

        Assert.True(result.Success, TestSite.Describe(result.Messages));
        Assert.False(await _site.QueryDatabaseAsync(db => db.Contents.AnyAsync(x => x.Id == page.Id)));
    }

    [Fact]
    public async Task Permanent_delete_is_refused_while_the_content_has_children()
    {
        var parent = await _site.CreateContentAsync(_pageType, "Parent");
        await _site.CreateContentAsync(_pageType, "Child", parent.Id);

        var result = await DeleteAsync(parent.Id, moveToRecycleBin: false);

        Assert.False(result.Success);
        Assert.True(await _site.QueryDatabaseAsync(db => db.Contents.AnyAsync(x => x.Id == parent.Id)));
    }

    private Task<HandlerResult<ContentItem>> RecycleAsync(Guid id) => DeleteAsync(id, moveToRecycleBin: true);

    private async Task<HandlerResult<ContentItem>> DeleteAsync(Guid id, bool moveToRecycleBin)
    {
        using var scope = _site.CreateScope();
        var contentService = scope.ServiceProvider.GetRequiredService<IContentService>();
        return await contentService.DeleteContentAsync(new DeleteContentParameters
        {
            ContentId = id,
            MoveToRecycleBin = moveToRecycleBin
        });
    }

    private async Task<ContentItem?> GetAsync(GetContentParameters parameters)
    {
        using var scope = _site.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IContentService>().GetContentAsync(parameters);
    }

    private async Task<List<ContentItem>> QueryAsync(QueryContentParameters parameters)
    {
        using var scope = _site.CreateScope();
        var page = await scope.ServiceProvider.GetRequiredService<IContentService>().QueryContentAsync(parameters);
        return page.Items.ToList();
    }
}

/// <summary>The same tests on a real PostgreSQL (Roadmap D15); skipped without Docker.</summary>
public class ContentSoftDeleteTestsOnPostgreSql : ContentSoftDeleteTests
{
    protected override string DatabaseProvider => TestSite.PostgreSqlProvider;
}

/// <summary>The same tests on SQL Server; skipped without <c>ZAUBER_TEST_SQLSERVER</c>.</summary>
public class ContentSoftDeleteTestsOnSqlServer : ContentSoftDeleteTests
{
    protected override string DatabaseProvider => TestSite.SqlServerProvider;
}
