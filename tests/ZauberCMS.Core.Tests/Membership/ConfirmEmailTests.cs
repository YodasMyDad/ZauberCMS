using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Membership.Interfaces;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Membership.Parameters;
using ZauberCMS.Core.Tests.Infrastructure;

namespace ZauberCMS.Core.Tests.Membership;

/// <summary>
/// The emailed confirmation link carries a Base64Url-encoded token; the service must accept the code exactly as
/// it is in the link (ZA-103).
/// </summary>
public class ConfirmEmailTests : IAsyncLifetime
{
    private TestSite _site = null!;

    public async ValueTask InitializeAsync() => _site = await TestSite.CreateAsync();

    public async ValueTask DisposeAsync() => await _site.DisposeAsync();

    [Fact]
    public async Task The_code_from_the_emailed_link_confirms_the_email()
    {
        var user = await _site.CreateUserAsync("newcomer");
        var code = await CodeAsInLinkAsync(user.Id, (manager, u) => manager.GenerateEmailConfirmationTokenAsync(u));

        var result = await ConfirmAsync(new ConfirmEmailParameters { UserId = user.Id.ToString(), Code = code });

        Assert.True(result.Success, TestSite.Describe(result.Messages));
        Assert.True(await _site.QueryDatabaseAsync(db =>
            db.Users.AsNoTracking().Where(x => x.Id == user.Id).Select(x => x.EmailConfirmed).SingleAsync()));
    }

    [Fact]
    public async Task The_code_from_a_change_of_email_link_changes_the_email()
    {
        const string newEmail = "moved@example.test";
        var user = await _site.CreateUserAsync("mover");
        var code = await CodeAsInLinkAsync(user.Id, (manager, u) => manager.GenerateChangeEmailTokenAsync(u, newEmail));

        var result = await ConfirmAsync(new ConfirmEmailParameters
        {
            UserId = user.Id.ToString(), Code = code, IsEmailUpdate = true, NewEmailAddress = newEmail
        });

        Assert.True(result.Success, TestSite.Describe(result.Messages));
        var stored = await _site.QueryDatabaseAsync(db =>
            db.Users.AsNoTracking().Where(x => x.Id == user.Id).Select(x => new { x.Email, x.EmailConfirmed }).SingleAsync());
        Assert.Equal(newEmail, stored.Email);
        Assert.True(stored.EmailConfirmed);
    }

    [Fact]
    public async Task A_change_of_email_code_is_refused_for_a_different_address()
    {
        var user = await _site.CreateUserAsync("mover");
        var code = await CodeAsInLinkAsync(user.Id,
            (manager, u) => manager.GenerateChangeEmailTokenAsync(u, "moved@example.test"));

        var result = await ConfirmAsync(new ConfirmEmailParameters
        {
            UserId = user.Id.ToString(), Code = code, IsEmailUpdate = true, NewEmailAddress = "attacker@example.test"
        });

        Assert.False(result.Success);
        Assert.Equal("mover@example.test", await _site.QueryDatabaseAsync(db =>
            db.Users.AsNoTracking().Where(x => x.Id == user.Id).Select(x => x.Email).SingleAsync()));
    }

    [Theory]
    [InlineData("not-a-real-code")]
    [InlineData("%%%")]
    [InlineData("")]
    public async Task A_wrong_or_malformed_code_is_refused_with_a_message(string code)
    {
        var user = await _site.CreateUserAsync("newcomer");

        var result = await ConfirmAsync(new ConfirmEmailParameters { UserId = user.Id.ToString(), Code = code });

        Assert.False(result.Success);
        Assert.NotEmpty(result.Messages);
        Assert.False(await _site.QueryDatabaseAsync(db =>
            db.Users.AsNoTracking().Where(x => x.Id == user.Id).Select(x => x.EmailConfirmed).SingleAsync()));
    }

    private async Task<string> CodeAsInLinkAsync(Guid userId, Func<UserManager<User>, User, Task<string>> generateToken)
    {
        using var scope = _site.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var user = await userManager.FindByIdAsync(userId.ToString());
        var token = await generateToken(userManager, user!);
        return WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
    }

    private async Task<AuthenticationResult> ConfirmAsync(ConfirmEmailParameters parameters)
    {
        using var scope = _site.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IMembershipService>().ConfirmEmailAsync(parameters);
    }
}
