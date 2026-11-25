using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Data.Interfaces;
using ZauberCMS.Core.Data.Parameters;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Shared.Models;
using ZauberCMS.Core.Tests.Infrastructure;

namespace ZauberCMS.Core.Tests.Data;

/// <summary>
/// Grid filter and sort strings are parsed into expressions. They may use the entity's ordinary columns, but never
/// its secrets, and never reach outside simple value types (ZA-020).
/// </summary>
public class DataGridFilterTests : IAsyncLifetime
{
    /// <summary>Overridden by the PostgreSQL copy of this class at the end of the file.</summary>
    protected virtual string DatabaseProvider => TestSite.SqliteProvider;

    private TestSite _site = null!;

    public async ValueTask InitializeAsync()
    {
        _site = await TestSite.CreateAsync(databaseProvider: DatabaseProvider);
        await _site.CreateUserAsync("stayer");
        await _site.CreateUserAsync("leaver");
    }

    public async ValueTask DisposeAsync() => await _site.DisposeAsync();

    [Theory]
    [InlineData("""x => x.UserName.Contains("stay")""")]
    [InlineData("""x => ((x.UserName ?? "").ToLower() ?? "").Contains("STAY".ToLower())""")]
    [InlineData("""x => x.UserName == "stayer" && x.EmailConfirmed == false""")]
    public async Task An_ordinary_column_filter_is_applied(string filter)
    {
        var grid = await GridAsync(new DataGridParameters<User> { Take = 100, Filter = filter });

        Assert.Equal(["stayer"], grid.Items.Select(x => x.UserName));
        Assert.Equal(1, grid.Count);
    }

    [Theory]
    [InlineData("UserName desc", "test-admin")]
    [InlineData("UserName asc", "leaver")]
    public async Task An_ordinary_column_sort_is_applied(string orderBy, string expectedFirst)
    {
        var grid = await GridAsync(new DataGridParameters<User> { Take = 100, OrderBy = orderBy });

        Assert.Equal(expectedFirst, grid.Items.First().UserName);
    }

    [Theory]
    [InlineData("""x => x.PasswordHash.StartsWith("A")""")]
    [InlineData("""x => x.PasswordHash != null""")]
    [InlineData("""x => x.SecurityStamp.Contains("A")""")]
    [InlineData("""x => x.ConcurrencyStamp == "A" """)]
    [InlineData("""x => x.GetType().Name == "User" """)]
    [InlineData("""x => x.UserName.GetType().Assembly.FullName != null""")]
    [InlineData("""x => System.IO.File.Exists("c:/windows/win.ini")""")]
    [InlineData("""this is not an expression""")]
    public async Task A_filter_on_a_secret_or_outside_the_entity_is_refused(string filter)
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            GridAsync(new DataGridParameters<User> { Take = 100, Filter = filter }));
    }

    [Theory]
    [InlineData("PasswordHash")]
    [InlineData("SecurityStamp desc")]
    public async Task A_sort_on_a_secret_is_refused(string orderBy)
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            GridAsync(new DataGridParameters<User> { Take = 100, OrderBy = orderBy }));
    }

    [Fact]
    public async Task The_content_grid_accepts_the_filter_and_sort_the_admin_grid_sends()
    {
        // The form Radzen's data grid builds: a lambda. An earlier text check refused every "=>" (ZA-139).
        var pages = await _site.CreateContentTypeAsync("page");
        await _site.CreateContentAsync(pages, "About us");
        await _site.CreateContentAsync(pages, "Contact");
        await _site.CreateContentAsync(pages, "About the team");

        using var scope = _site.CreateScope();
        var grid = await scope.ServiceProvider.GetRequiredService<ZauberCMS.Core.Content.Interfaces.IContentService>()
            .GetDataGridContentAsync(new ZauberCMS.Core.Content.Parameters.DataGridContentParameters
            {
                Take = 100,
                Filter = """x => ((x.Name ?? "").ToLower() ?? "").Contains("ABOUT".ToLower())""",
                Order = "Name desc"
            }, TestContext.Current.CancellationToken);

        Assert.Equal(["About us", "About the team"], grid.Items.Select(x => x.Name));
        Assert.Equal(2, grid.Count);
    }

    private async Task<DataGridResult<User>> GridAsync(DataGridParameters<User> parameters)
    {
        using var scope = _site.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IDataService>()
            .GetDataGridAsync(parameters, TestContext.Current.CancellationToken);
    }
}

/// <summary>The same tests on a real PostgreSQL (Roadmap D15); skipped without Docker.</summary>
public class DataGridFilterTestsOnPostgreSql : DataGridFilterTests
{
    protected override string DatabaseProvider => TestSite.PostgreSqlProvider;
}
