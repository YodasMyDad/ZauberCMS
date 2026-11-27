using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Membership;
using ZauberCMS.Core.Membership.Interfaces;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Membership.Parameters;
using ZauberCMS.Core.Tests.Infrastructure;

namespace ZauberCMS.Core.Tests.Membership;

/// <summary>
/// Failed passwords count towards lockout (ZA-059), and the ticket that lets a user's cookie be re-issued after
/// they changed their own account is bound to that user and that change (ZA-052).
/// </summary>
public class SignInHardeningTests : IAsyncLifetime
{
    private TestSite _site = null!;

    public async ValueTask InitializeAsync() => _site = await TestSite.CreateAsync();

    public async ValueTask DisposeAsync() => await _site.DisposeAsync();

    [Fact]
    public async Task Repeated_wrong_passwords_lock_the_account_and_the_right_password_is_then_refused()
    {
        var user = await _site.CreateUserAsync("target");
        var maxAttempts = _site.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<IdentityOptions>>()
            .Value.Lockout.MaxFailedAccessAttempts;

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            Assert.False((await LoginAsync(user.Email!, "wrong-password")).Success);
        }

        using (var scope = _site.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            Assert.True(await userManager.IsLockedOutAsync((await userManager.FindByIdAsync(user.Id.ToString()))!));
        }

        var afterLockout = await LoginAsync(user.Email!, "Passw0rd-For-Tests");
        Assert.False(afterLockout.Success);
        Assert.Contains(afterLockout.Messages, m => m.Message!.Contains("Too many failed sign-in attempts"));
    }

    [Fact]
    public async Task The_right_password_signs_in()
    {
        var user = await _site.CreateUserAsync("regular");

        var result = await LoginAsync(user.Email!, "Passw0rd-For-Tests");

        Assert.True(result.Success, TestSite.Describe(result.Messages));
    }

    [Fact]
    public async Task A_refresh_ticket_is_valid_for_its_user_until_the_security_stamp_changes()
    {
        var tickets = _site.Services.GetRequiredService<RefreshSignInTicketService>();
        using var scope = _site.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var user = (await userManager.FindByIdAsync((await _site.CreateUserAsync("owner")).Id.ToString()))!;
        var other = (await userManager.FindByIdAsync((await _site.CreateUserAsync("other")).Id.ToString()))!;

        var ticket = tickets.Create(user);

        Assert.True(tickets.IsValid(ticket, user));
        Assert.False(tickets.IsValid(ticket, other));
        Assert.False(tickets.IsValid("not-a-ticket", user));
        Assert.False(tickets.IsValid(null, user));

        // A password reset, a delete or any other stamp change makes the ticket worthless.
        await userManager.UpdateSecurityStampAsync(user);
        Assert.False(tickets.IsValid(ticket, user));
    }

    [Fact]
    public async Task Saving_your_own_user_name_hands_out_a_refresh_ticket_and_saving_another_user_does_not()
    {
        var tickets = _site.Services.GetRequiredService<RefreshSignInTicketService>();
        var another = await _site.CreateUserAsync("another");

        var own = await RenameAsync(_site.CurrentUser.Id, "test-admin-renamed");
        var others = await RenameAsync(another.Id, "another-renamed");

        Assert.True(own.Success, TestSite.Describe(own.Messages));
        Assert.True(own.RefreshSignIn);
        Assert.True(tickets.IsValid(own.RefreshSignInTicket, own.Entity!));
        Assert.True(others.Success, TestSite.Describe(others.Messages));
        Assert.False(others.RefreshSignIn);
        Assert.Null(others.RefreshSignInTicket);
    }

    private async Task<ZauberCMS.Core.Shared.Models.HandlerResult<User>> RenameAsync(Guid userId, string userName)
    {
        using var scope = _site.CreateScope();
        var membership = scope.ServiceProvider.GetRequiredService<IMembershipService>();
        var user = (await scope.ServiceProvider.GetRequiredService<UserManager<User>>().FindByIdAsync(userId.ToString()))!;
        user.UserName = userName;
        return await membership.SaveUserAsync(new SaveUserParameters { User = user });
    }

    private async Task<AuthenticationResult> LoginAsync(string email, string password)
    {
        using var scope = _site.CreateScope();
        // Sign-in writes cookies, so it needs a request to write them to.
        scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext =
            new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        return await scope.ServiceProvider.GetRequiredService<IMembershipService>().LoginUserAsync(
            new LoginUserParameters { Email = email, Password = password }, TestContext.Current.CancellationToken);
    }
}

/// <summary>
/// ZA-106: only someone who knows the password learns that an account is waiting for email confirmation, and
/// signing in does not send the confirmation email again and again.
/// </summary>
public class UnconfirmedSignInTests : IAsyncLifetime
{
    private TestSite _site = null!;

    public async ValueTask InitializeAsync() => _site = await TestSite.CreateAsync(new Dictionary<string, string?>
    {
        ["Zauber:Identity:SignInRequireConfirmedAccount"] = "true",
        ["Zauber:Plugins:EmailProvider"] = typeof(RecordingEmailProvider).FullName,
        ["Zauber:SiteUrl"] = "https://site.test"
    });

    public async ValueTask DisposeAsync() => await _site.DisposeAsync();

    [Fact]
    public async Task A_wrong_password_on_an_unconfirmed_account_says_nothing_about_it_and_sends_nothing()
    {
        var user = await _site.CreateUserAsync($"unconfirmed-{Guid.NewGuid():N}");

        var result = await LoginAsync(user.Email!, "wrong-password");

        Assert.False(result.Success);
        Assert.Contains(result.Messages, m => m.Message == "Invalid login credentials");
        Assert.DoesNotContain(result.Messages, m => m.Message!.Contains("confirmed"));
        Assert.DoesNotContain(RecordingEmailProvider.Sent, s => s.To == user.Email);
        using var scope = _site.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        Assert.Equal(1, await userManager.GetAccessFailedCountAsync((await userManager.FindByIdAsync(user.Id.ToString()))!));
    }

    [Fact]
    public async Task The_right_password_on_an_unconfirmed_account_explains_and_sends_the_email_once()
    {
        var user = await _site.CreateUserAsync($"unconfirmed-{Guid.NewGuid():N}");

        var first = await LoginAsync(user.Email!, "Passw0rd-For-Tests");
        var second = await LoginAsync(user.Email!, "Passw0rd-For-Tests");

        Assert.False(first.Success);
        Assert.Contains(first.Messages, m => m.Message!.Contains("isn't confirmed"));
        Assert.Contains(second.Messages, m => m.Message!.Contains("isn't confirmed"));
        Assert.Single(RecordingEmailProvider.Sent, s => s.To == user.Email);
    }

    [Fact]
    public async Task A_locked_unconfirmed_account_is_not_tried_so_its_password_cannot_be_guessed()
    {
        var user = await _site.CreateUserAsync($"unconfirmed-{Guid.NewGuid():N}");
        using (var scope = _site.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            await userManager.SetLockoutEndDateAsync((await userManager.FindByIdAsync(user.Id.ToString()))!,
                DateTimeOffset.UtcNow.AddMinutes(5));
        }

        var result = await LoginAsync(user.Email!, "Passw0rd-For-Tests");

        Assert.False(result.Success);
        Assert.Contains(result.Messages, m => m.Message!.Contains("Too many failed sign-in attempts"));
        Assert.DoesNotContain(result.Messages, m => m.Message!.Contains("confirmed"));
    }

    [Fact]
    public async Task Wrong_passwords_lock_an_unconfirmed_account_like_any_other()
    {
        var user = await _site.CreateUserAsync($"unconfirmed-{Guid.NewGuid():N}");
        var maxAttempts = _site.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<IdentityOptions>>()
            .Value.Lockout.MaxFailedAccessAttempts;
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            await LoginAsync(user.Email!, "wrong-password");
        }

        var result = await LoginAsync(user.Email!, "Passw0rd-For-Tests");

        Assert.Contains(result.Messages, m => m.Message!.Contains("Too many failed sign-in attempts"));
    }

    private async Task<AuthenticationResult> LoginAsync(string email, string password)
    {
        using var scope = _site.CreateScope();
        scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext =
            new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        return await scope.ServiceProvider.GetRequiredService<IMembershipService>().LoginUserAsync(
            new LoginUserParameters { Email = email, Password = password }, TestContext.Current.CancellationToken);
    }
}