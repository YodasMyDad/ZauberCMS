using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Membership.Interfaces;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Membership.Parameters;
using ZauberCMS.Core.Shared.Models;
using ZauberCMS.Core.Tests.Infrastructure;

namespace ZauberCMS.Core.Tests.Membership;

/// <summary>Deleting a user is stored (ZA-045, ZA-088) and erases the person (ZA-033, D35).</summary>
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
    public async Task ZA033_delete_erases_the_person_and_keeps_the_row()
    {
        var user = await _site.CreateUserAsync("leaver");
        using (var scope = _site.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<User>>();
            var stored = (await userManager.FindByIdAsync(user.Id.ToString()))!;
            await userManager.SetPhoneNumberAsync(stored, "+44 1234 567890");
            await userManager.AddLoginAsync(stored, new Microsoft.AspNetCore.Identity.UserLoginInfo("Google", "g-1", "Google"));
            await userManager.AddClaimAsync(stored, new System.Security.Claims.Claim("nickname", "Lee"));
        }

        var result = await DeleteAsync(new DeleteUserParameters { Id = user.Id });
        Assert.True(result.Success, TestSite.Describe(result.Messages));

        var erased = await _site.QueryDatabaseAsync(db => db.Users.AsNoTracking().SingleAsync(x => x.Id == user.Id));
        Assert.True(erased.Deleted);
        Assert.Equal($"deleted-{user.Id:N}", erased.UserName);
        Assert.Null(erased.Email);
        Assert.Null(erased.NormalizedEmail);
        Assert.Null(erased.PhoneNumber);
        Assert.Null(erased.PasswordHash);
        Assert.Equal(0, await _site.QueryDatabaseAsync(db => db.UserLogins.CountAsync(x => x.UserId == user.Id)));
        Assert.Equal(0, await _site.QueryDatabaseAsync(db => db.UserClaims.CountAsync(x => x.UserId == user.Id)));
        Assert.Equal(0, await _site.QueryDatabaseAsync(db => db.UserRoles.CountAsync(x => x.UserId == user.Id)));
    }

    [Fact]
    public async Task ZA033_the_address_and_name_of_a_deleted_user_can_register_again()
    {
        var user = await _site.CreateUserAsync("leaver");
        await DeleteAsync(new DeleteUserParameters { Id = user.Id });

        var again = await _site.CreateUserAsync("leaver");

        Assert.NotEqual(user.Id, again.Id);
        Assert.Equal("leaver@example.test", again.Email);
    }

    [Fact]
    public async Task ZA033_a_deleted_user_cannot_be_deleted_again()
    {
        var user = await _site.CreateUserAsync("leaver");
        await DeleteAsync(new DeleteUserParameters { Id = user.Id });

        Assert.False((await DeleteAsync(new DeleteUserParameters { Id = user.Id })).Success);
    }

    [Fact]
    public async Task ZA033_you_cannot_delete_your_own_account()
    {
        var result = await DeleteAsync(new DeleteUserParameters { Id = _site.CurrentUser.Id });

        Assert.False(result.Success);
        Assert.False(await IsDeletedAsync(_site.CurrentUser.Id));
    }

    [Fact]
    public async Task ZA033_the_last_administrator_cannot_be_deleted_until_there_is_another()
    {
        var boss = await _site.CreateUserAsync("boss");
        var deputy = await _site.CreateUserAsync("deputy");
        await AddToAdminAsync(boss);

        var refused = await DeleteAsync(new DeleteUserParameters { Id = boss.Id });
        Assert.False(refused.Success);
        Assert.False(await IsDeletedAsync(boss.Id));

        await AddToAdminAsync(deputy);
        var deleted = await DeleteAsync(new DeleteUserParameters { Id = boss.Id });
        Assert.True(deleted.Success, TestSite.Describe(deleted.Messages));
    }

    [Fact]
    public async Task ZA033_delete_removes_profile_values_and_tokens_too()
    {
        var user = await _site.CreateUserAsync("leaver");
        using (var scope = _site.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<User>>();
            var stored = (await userManager.FindByIdAsync(user.Id.ToString()))!;
            await userManager.SetAuthenticationTokenAsync(stored, "Google", "refresh", "secret-token");
        }

        await _site.QueryDatabaseAsync(async db =>
        {
            db.UserPropertyValues.Add(new UserPropertyValue
            {
                UserId = user.Id, Alias = "bio", Value = "Likes long walks", ContentTypePropertyId = Guid.NewGuid()
            });
            return await db.SaveChangesAsync();
        });

        Assert.True((await DeleteAsync(new DeleteUserParameters { Id = user.Id })).Success);

        Assert.Equal(0, await _site.QueryDatabaseAsync(db => db.UserPropertyValues.CountAsync(x => x.UserId == user.Id)));
        Assert.Equal(0, await _site.QueryDatabaseAsync(db => db.UserTokens.CountAsync(x => x.UserId == user.Id)));
    }

    [Fact]
    public async Task ZA033_somebody_who_took_the_erased_name_first_does_not_stop_the_erasure()
    {
        var user = await _site.CreateUserAsync("leaver");
        await _site.CreateUserAsync($"deleted-{user.Id:N}");

        var result = await DeleteAsync(new DeleteUserParameters { Id = user.Id });

        Assert.True(result.Success, TestSite.Describe(result.Messages));
        Assert.True(await IsDeletedAsync(user.Id));
    }

    private async Task AddToAdminAsync(User user)
    {
        using var scope = _site.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.RoleManager<Role>>();
        if (!await roleManager.RoleExistsAsync(ZauberCMS.Core.Constants.Roles.AdminRoleName))
        {
            await roleManager.CreateAsync(new Role { Name = ZauberCMS.Core.Constants.Roles.AdminRoleName });
        }

        var userManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<User>>();
        var added = await userManager.AddToRoleAsync((await userManager.FindByIdAsync(user.Id.ToString()))!,
            ZauberCMS.Core.Constants.Roles.AdminRoleName);
        Assert.True(added.Succeeded, string.Join("; ", added.Errors.Select(e => e.Description)));
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
