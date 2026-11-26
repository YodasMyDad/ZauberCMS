using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Membership.Interfaces;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Membership.Parameters;
using ZauberCMS.Core.Tests.Infrastructure;

namespace ZauberCMS.Core.Tests.Membership;

/// <summary>
/// ZA-117 (Roadmap Phase 10b): a user save takes the profile fields from the caller's copy, never the sign-in and
/// security fields, so a copy opened before a password change or a lockout can't put the old values back.
/// </summary>
public class UserSaveTests : IAsyncLifetime
{
    private TestSite _site = null!;

    public async ValueTask InitializeAsync() => _site = await TestSite.CreateAsync();

    public async ValueTask DisposeAsync() => await _site.DisposeAsync();

    [Fact]
    public async Task ZA117_Saving_an_old_copy_keeps_a_password_change_and_a_lockout_made_since()
    {
        var ct = TestContext.Current.CancellationToken;
        var member = await _site.CreateUserAsync("member");
        // The editor opens the user
        var editorCopy = await _site.QueryDatabaseAsync(db =>
            db.Users.AsNoTracking().SingleAsync(x => x.Id == member.Id, ct));

        // Meanwhile the user changes their password and someone locks the account
        string newHash;
        DateTimeOffset? lockedUntil;
        using (var scope = _site.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var user = (await userManager.FindByIdAsync(member.Id.ToString()))!;
            Assert.True((await userManager.ChangePasswordAsync(user, "Passw0rd-For-Tests", "An0ther-Passw0rd")).Succeeded);
            Assert.True((await userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddDays(1))).Succeeded);
            newHash = user.PasswordHash!;
            lockedUntil = user.LockoutEnd;
        }

        editorCopy.PhoneNumber = "0123 456789";
        using (var scope = _site.CreateScope())
        {
            var saved = await scope.ServiceProvider.GetRequiredService<IMembershipService>()
                .SaveUserAsync(new SaveUserParameters { User = editorCopy }, ct);
            Assert.True(saved.Success, TestSite.Describe(saved.Messages));
        }

        var stored = await _site.QueryDatabaseAsync(db => db.Users.AsNoTracking().SingleAsync(x => x.Id == member.Id, ct));
        Assert.Equal("0123 456789", stored.PhoneNumber);
        Assert.Equal(newHash, stored.PasswordHash);
        Assert.Equal(lockedUntil, stored.LockoutEnd);
    }
}
