using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Membership.Interfaces;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Membership.Parameters;
using ZauberCMS.Core.Shared.Models;
using ZauberCMS.Core.Tests.Infrastructure;

namespace ZauberCMS.Core.Tests.Membership;

/// <summary>Deleting a user is a soft delete that must really be stored (ZA-045, ZA-088).</summary>
public class DeleteUserTests : IAsyncLifetime
{
    /// <summary>Overridden by the PostgreSQL copy of this class at the end of the file.</summary>
    protected virtual string DatabaseProvider => TestSite.SqliteProvider;

    private TestSite _site = null!;

    public async ValueTask InitializeAsync() => _site = await TestSite.CreateAsync(databaseProvider: DatabaseProvider);

    public async ValueTask DisposeAsync() => await _site.DisposeAsync();

    [Fact]
    public async Task Delete_by_Id_flags_the_user_deleted_in_the_database()
    {
        var user = await _site.CreateUserAsync("leaver");

        var result = await DeleteAsync(new DeleteUserParameters { Id = user.Id });

        Assert.True(result.Success, TestSite.Describe(result.Messages));
        Assert.True(await IsDeletedAsync(user.Id));
    }

    [Fact]
    public async Task Delete_changes_the_security_stamp_so_existing_sessions_end()
    {
        var user = await _site.CreateUserAsync("leaver");
        var stampBefore = await StampAsync(user.Id);

        await DeleteAsync(new DeleteUserParameters { Id = user.Id });

        Assert.NotEqual(stampBefore, await StampAsync(user.Id));
    }

    // UserId is the obsolete name of Id; sites compiled against it must still delete the user (ZA-045).
    [Fact]
    public async Task Delete_by_the_obsolete_UserId_still_flags_the_user_deleted_in_the_database()
    {
        var user = await _site.CreateUserAsync("leaver");

#pragma warning disable CS0618
        var result = await DeleteAsync(new DeleteUserParameters { UserId = user.Id });
#pragma warning restore CS0618

        Assert.True(result.Success, TestSite.Describe(result.Messages));
        Assert.True(await IsDeletedAsync(user.Id));
    }

    [Fact]
    public async Task Delete_of_an_unknown_user_reports_failure()
    {
        var result = await DeleteAsync(new DeleteUserParameters { Id = Guid.NewGuid() });

        Assert.False(result.Success);
        Assert.NotEmpty(result.Messages);
    }

    private async Task<HandlerResult<User>> DeleteAsync(DeleteUserParameters parameters)
    {
        using var scope = _site.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IMembershipService>().DeleteUserAsync(parameters);
    }

    private Task<bool> IsDeletedAsync(Guid userId) =>
        _site.QueryDatabaseAsync(db =>
            db.Users.AsNoTracking().Where(x => x.Id == userId).Select(x => x.Deleted).SingleAsync());

    private Task<string?> StampAsync(Guid userId) =>
        _site.QueryDatabaseAsync(db =>
            db.Users.AsNoTracking().Where(x => x.Id == userId).Select(x => x.SecurityStamp).SingleAsync());
}

/// <summary>The same tests on a real PostgreSQL (Roadmap D15); skipped without Docker.</summary>
public class DeleteUserTestsOnPostgreSql : DeleteUserTests
{
    protected override string DatabaseProvider => TestSite.PostgreSqlProvider;
}

/// <summary>The same tests on SQL Server; skipped without <c>ZAUBER_TEST_SQLSERVER</c>.</summary>
public class DeleteUserTestsOnSqlServer : DeleteUserTests
{
    protected override string DatabaseProvider => TestSite.SqlServerProvider;
}
