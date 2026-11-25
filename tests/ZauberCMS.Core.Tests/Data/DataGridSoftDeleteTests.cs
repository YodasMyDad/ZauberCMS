using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Content.Interfaces;
using ZauberCMS.Core.Content.Parameters;
using ZauberCMS.Core.Data.Interfaces;
using ZauberCMS.Core.Data.Parameters;
using ZauberCMS.Core.Membership.Interfaces;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Membership.Parameters;
using ZauberCMS.Core.Tests.Infrastructure;
using ContentItem = ZauberCMS.Core.Content.Models.Content;

namespace ZauberCMS.Core.Tests.Data;

/// <summary>
/// The admin dashboards list rows through the generic data grid query. There are no global query filters, so it
/// must leave out soft-deleted users and content itself (AGENTS.md rule 4, ZA-137).
/// </summary>
public class DataGridSoftDeleteTests : IAsyncLifetime
{
    /// <summary>Overridden by the PostgreSQL copy of this class at the end of the file.</summary>
    protected virtual string DatabaseProvider => TestSite.SqliteProvider;

    private TestSite _site = null!;

    public async ValueTask InitializeAsync() => _site = await TestSite.CreateAsync(databaseProvider: DatabaseProvider);

    public async ValueTask DisposeAsync() => await _site.DisposeAsync();

    [Fact]
    public async Task A_deleted_user_is_not_listed_and_not_counted()
    {
        var kept = await _site.CreateUserAsync("stayer");
        var deleted = await _site.CreateUserAsync("leaver");
        await DeleteUserAsync(deleted.Id);

        var grid = await GridAsync(new DataGridParameters<User> { Take = 100 });

        Assert.Contains(grid.Items, x => x.Id == kept.Id);
        Assert.DoesNotContain(grid.Items, x => x.Id == deleted.Id);
        Assert.Equal(grid.Items.Count(), grid.Count);
    }

    [Fact]
    public async Task A_deleted_user_is_listed_when_deleted_rows_are_asked_for()
    {
        var deleted = await _site.CreateUserAsync("leaver");
        await DeleteUserAsync(deleted.Id);

        var grid = await GridAsync(new DataGridParameters<User> { Take = 100, IncludeDeleted = true });

        Assert.Contains(grid.Items, x => x.Id == deleted.Id);
    }

    [Fact]
    public async Task Recycled_content_is_not_listed()
    {
        var pageType = await _site.CreateContentTypeAsync("page");
        var kept = await _site.CreateContentAsync(pageType, "Kept");
        var recycled = await _site.CreateContentAsync(pageType, "Recycled");
        using (var scope = _site.CreateScope())
        {
            var result = await scope.ServiceProvider.GetRequiredService<IContentService>().DeleteContentAsync(
                new DeleteContentParameters { ContentId = recycled.Id, MoveToRecycleBin = true },
                TestContext.Current.CancellationToken);
            Assert.True(result.Success, TestSite.Describe(result.Messages));
        }

        var grid = await GridAsync(new DataGridParameters<ContentItem> { Take = 100 });

        Assert.Contains(grid.Items, x => x.Id == kept.Id);
        Assert.DoesNotContain(grid.Items, x => x.Id == recycled.Id);
    }

    private async Task DeleteUserAsync(Guid id)
    {
        using var scope = _site.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<IMembershipService>()
            .DeleteUserAsync(new DeleteUserParameters { Id = id }, TestContext.Current.CancellationToken);
        Assert.True(result.Success, TestSite.Describe(result.Messages));
    }

    private async Task<ZauberCMS.Core.Shared.Models.DataGridResult<T>> GridAsync<T>(DataGridParameters<T> parameters)
        where T : class, ZauberCMS.Core.Shared.Interfaces.ITreeItem
    {
        using var scope = _site.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IDataService>()
            .GetDataGridAsync(parameters, TestContext.Current.CancellationToken);
    }
}

/// <summary>The same tests on a real PostgreSQL (Roadmap D15); skipped without Docker.</summary>
public class DataGridSoftDeleteTestsOnPostgreSql : DataGridSoftDeleteTests
{
    protected override string DatabaseProvider => TestSite.PostgreSqlProvider;
}

/// <summary>The same tests on SQL Server; skipped without <c>ZAUBER_TEST_SQLSERVER</c>.</summary>
public class DataGridSoftDeleteTestsOnSqlServer : DataGridSoftDeleteTests
{
    protected override string DatabaseProvider => TestSite.SqlServerProvider;
}
