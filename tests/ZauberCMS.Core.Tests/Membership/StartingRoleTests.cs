using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Data.Interfaces;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Installer.Interfaces;
using ZauberCMS.Core.Membership.Interfaces;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Membership.Parameters;
using ZauberCMS.Core.Settings;
using ZauberCMS.Core.Tests.Infrastructure;

namespace ZauberCMS.Core.Tests.Membership;

/// <summary>
/// ZA-004: registering never makes an administrator by being first. ZA-023: an address on the admin list is
/// promoted only once it is confirmed. D21: the recovery key.
/// </summary>
public class StartingRoleTests : IAsyncLifetime
{
    private TestSite _site = null!;

    public async ValueTask InitializeAsync() => _site = await TestSite.CreateAsync(createUser: false);

    public async ValueTask DisposeAsync() => await _site.DisposeAsync();

    private async Task<User> RegisterAsync(string name, string email)
    {
        using var scope = _site.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<IMembershipService>().RegisterUserAsync(
            new RegisterUserParameters
            {
                Username = name,
                Email = email,
                Password = "Passw0rd-For-Tests",
                // Signing in needs a request; these tests stop at the account
                AutoLogin = false
            }, TestContext.Current.CancellationToken);
        Assert.True(result.Success, TestSite.Describe(result.Messages));
        var user = await scope.ServiceProvider.GetRequiredService<UserManager<User>>().FindByEmailAsync(email);
        return Assert.IsType<User>(user);
    }

    private async Task<IList<string>> RolesAsync(User user)
    {
        using var scope = _site.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        return await userManager.GetRolesAsync((await userManager.FindByIdAsync(user.Id.ToString()))!);
    }

    private async Task ListAsAdminAsync(params string[] addresses)
    {
        using var scope = _site.CreateScope();
        var dataService = scope.ServiceProvider.GetRequiredService<IDataService>();
        var settings = await dataService.GetGlobalSettings();
        settings.AdminEmailAddresses = [.. addresses];
        Assert.True(await dataService.SaveGlobalSettings(settings));
    }

    private async Task ConfirmEmailAsync(User user)
    {
        using var scope = _site.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var stored = (await userManager.FindByIdAsync(user.Id.ToString()))!;
        var code = WebEncoders.Base64UrlEncode(
            Encoding.UTF8.GetBytes(await userManager.GenerateEmailConfirmationTokenAsync(stored)));
        var result = await scope.ServiceProvider.GetRequiredService<IMembershipService>().ConfirmEmailAsync(
            new ConfirmEmailParameters { UserId = user.Id.ToString(), Code = code },
            TestContext.Current.CancellationToken);
        Assert.True(result.Success, TestSite.Describe(result.Messages));
    }

    [Fact]
    public async Task The_first_account_to_register_is_an_ordinary_member()
    {
        var first = await RegisterAsync("first", "first@example.test");

        Assert.Equal(["Member"], await RolesAsync(first));
    }

    [Fact]
    public async Task The_only_account_left_after_every_other_was_deleted_is_still_an_ordinary_member()
    {
        var gone = await _site.CreateUserAsync("gone");
        await _site.QueryDatabaseAsync(async db =>
        {
            (await db.Users.FindAsync(gone.Id))!.Deleted = true;
            return await db.SaveChangesAsync();
        });

        var newcomer = await RegisterAsync("newcomer", "newcomer@example.test");

        Assert.Equal(["Member"], await RolesAsync(newcomer));
    }

    // The attack ZA-023 is about: a stranger registers with an administrator's address and their own password,
    // and the administrator (or a mail scanner) opens the confirmation link
    [Fact]
    public async Task Opening_the_confirmation_link_does_not_promote_an_address_on_the_admin_list()
    {
        await ListAsAdminAsync("boss@example.test");
        var squatter = await RegisterAsync("squatter", "boss@example.test");
        Assert.Equal(["Member"], await RolesAsync(squatter));

        await ConfirmEmailAsync(squatter);

        Assert.Equal(["Member"], await RolesAsync(squatter));
    }

    [Fact]
    public async Task Registering_with_a_listed_address_emails_its_owner_a_link_to_choose_the_password()
    {
        await using var site = await TestSite.CreateAsync(new Dictionary<string, string?>
        {
            ["Zauber:Plugins:EmailProvider"] = typeof(RecordingEmailProvider).FullName,
            ["Zauber:SiteUrl"] = "https://site.test"
        }, createUser: false);
        const string listed = "boss-mail-test@example.test";
        using (var scope = site.CreateScope())
        {
            var dataService = scope.ServiceProvider.GetRequiredService<IDataService>();
            var settings = await dataService.GetGlobalSettings();
            settings.AdminEmailAddresses = [listed];
            Assert.True(await dataService.SaveGlobalSettings(settings));

            // The link is built for the request's site
            scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Http.IHttpContextAccessor>().HttpContext =
                new Microsoft.AspNetCore.Http.DefaultHttpContext { RequestServices = scope.ServiceProvider };
            var membership = scope.ServiceProvider.GetRequiredService<IMembershipService>();
            foreach (var (name, email) in new[] { ("squatter", listed), ("visitor", "visitor-mail-test@example.test") })
            {
                var result = await membership.RegisterUserAsync(new RegisterUserParameters
                {
                    Username = name, Email = email, Password = "Passw0rd-For-Tests", AutoLogin = false
                }, TestContext.Current.CancellationToken);
                Assert.True(result.Success, TestSite.Describe(result.Messages));
            }
        }

        var mail = Assert.Single(RecordingEmailProvider.Sent, m => m.To == listed);
        Assert.Contains("https://site.test/account/resetpassword", string.Join(" ", mail.Paragraphs),
            StringComparison.OrdinalIgnoreCase);
        // Nobody else gets one: this site does not ask for confirmation
        Assert.DoesNotContain(RecordingEmailProvider.Sent, m => m.To == "visitor-mail-test@example.test");
    }

    [Fact]
    public async Task Setting_the_password_from_the_emailed_link_promotes_an_address_on_the_admin_list()
    {
        await ListAsAdminAsync("boss@example.test");
        var account = await RegisterAsync("boss", "boss@example.test");

        await SetPasswordFromEmailAsync(account, "Chosen-By-The-0wner");

        Assert.Contains("Admin", await RolesAsync(account));
        using var scope = _site.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var stored = (await userManager.FindByIdAsync(account.Id.ToString()))!;
        Assert.True(stored.EmailConfirmed);
        // The account now belongs to whoever read the email: the password the registrant chose is gone
        Assert.True(await userManager.CheckPasswordAsync(stored, "Chosen-By-The-0wner"));
        Assert.False(await userManager.CheckPasswordAsync(stored, "Passw0rd-For-Tests"));
    }

    [Fact]
    public async Task The_admin_list_ignores_case_and_surrounding_spaces()
    {
        await ListAsAdminAsync("  Boss@Example.Test ");
        var boss = await RegisterAsync("boss", "boss@example.test");

        await SetPasswordFromEmailAsync(boss, "Chosen-By-The-0wner");

        Assert.Contains("Admin", await RolesAsync(boss));
        Assert.True(new GlobalSettings { AdminEmailAddresses = ["a@b.test"] }.IsAdminEmailAddress(" A@B.TEST "));
        Assert.False(new GlobalSettings { AdminEmailAddresses = ["a@b.test"] }.IsAdminEmailAddress(null));
        Assert.False(new GlobalSettings().IsAdminEmailAddress(""));
    }

    [Fact]
    public async Task Changing_to_a_listed_address_and_confirming_it_does_not_promote()
    {
        await ListAsAdminAsync("boss@example.test");
        var member = await RegisterAsync("member", "member@example.test");

        using (var scope = _site.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var stored = (await userManager.FindByIdAsync(member.Id.ToString()))!;
            var code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(
                await userManager.GenerateChangeEmailTokenAsync(stored, "boss@example.test")));

            var result = await scope.ServiceProvider.GetRequiredService<IMembershipService>().ConfirmEmailAsync(
                new ConfirmEmailParameters
                {
                    UserId = member.Id.ToString(),
                    Code = code,
                    IsEmailUpdate = true,
                    NewEmailAddress = "boss@example.test"
                }, TestContext.Current.CancellationToken);
            Assert.True(result.Success, TestSite.Describe(result.Messages));
        }

        Assert.Equal(["Member"], await RolesAsync(member));
    }

    [Fact]
    public async Task An_address_that_is_not_on_the_list_is_promoted_by_nothing()
    {
        await ListAsAdminAsync("boss@example.test");
        var visitor = await RegisterAsync("visitor", "visitor@example.test");

        await ConfirmEmailAsync(visitor);
        await SetPasswordFromEmailAsync(visitor, "Chosen-By-The-0wner");

        Assert.Equal(["Member"], await RolesAsync(visitor));
    }

    // What the page behind the emailed "choose a password" / "forgot your password" link does
    private async Task SetPasswordFromEmailAsync(User user, string password)
    {
        using var scope = _site.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var stored = (await userManager.FindByIdAsync(user.Id.ToString()))!;
        var result = await scope.ServiceProvider.GetRequiredService<IMembershipService>().ResetPasswordAsync(
            new ResetPasswordParameters
            {
                Email = stored.Email,
                Code = await userManager.GeneratePasswordResetTokenAsync(stored),
                Password = password,
                ConfirmPassword = password
            }, TestContext.Current.CancellationToken);
        Assert.True(result.Success, TestSite.Describe(result.Messages));
    }
}

/// <summary>D21: <c>Zauber:Identity:RecoverAdminUser</c> gives a site without an administrator one back.</summary>
public class RecoverAdminTests
{
    private static async Task<TestSite> SiteAsync(string recover) => await TestSite.CreateAsync(
        new Dictionary<string, string?> { ["Zauber:Identity:RecoverAdminUser"] = recover }, createUser: false);

    private static async Task InitialiseAsync(TestSite site)
    {
        using var scope = site.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IInstallService>()
            .InitialiseDatabaseAsync(TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData("lost@example.test")]
    [InlineData("LOST@example.test")]
    [InlineData("lost")]
    public async Task The_named_user_is_restored_unlocked_and_made_an_administrator(string recover)
    {
        await using var site = await SiteAsync(recover);
        var lost = await site.CreateUserAsync("lost");
        var bystander = await site.CreateUserAsync("bystander");
        await site.QueryDatabaseAsync(async db =>
        {
            var user = (await db.Users.FindAsync(lost.Id))!;
            user.Deleted = true;
            user.LockoutEnd = DateTimeOffset.UtcNow.AddYears(1);
            user.AccessFailedCount = 5;
            return await db.SaveChangesAsync();
        });

        await InitialiseAsync(site);
        // Leaving the key in place must not fail on the next start
        await InitialiseAsync(site);

        using var scope = site.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var recovered = (await userManager.FindByIdAsync(lost.Id.ToString()))!;
        Assert.False(recovered.Deleted);
        // Able to sign in also where confirmed accounts are required
        Assert.True(recovered.EmailConfirmed);
        Assert.Null(recovered.LockoutEnd);
        Assert.Equal(0, recovered.AccessFailedCount);
        Assert.Equal(["Admin"], await userManager.GetRolesAsync(recovered));
        Assert.Empty(await userManager.GetRolesAsync((await userManager.FindByIdAsync(bystander.Id.ToString()))!));
    }

    [Fact]
    public async Task An_email_address_in_the_key_does_not_match_somebody_who_took_it_as_a_user_name()
    {
        await using var site = await SiteAsync("boss@example.test");
        // "boss@example.test" as a user name; the account's email address is another one
        var impostor = new User { UserName = "boss@example.test", Email = "impostor@example.test" };
        using (var setup = site.CreateScope())
        {
            var created = await setup.ServiceProvider.GetRequiredService<UserManager<User>>()
                .CreateAsync(impostor, "Passw0rd-For-Tests");
            Assert.True(created.Succeeded, string.Join("; ", created.Errors.Select(e => e.Description)));
        }

        await InitialiseAsync(site);

        using var scope = site.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        Assert.Empty(await userManager.GetRolesAsync((await userManager.FindByIdAsync(impostor.Id.ToString()))!));
    }

    [Fact]
    public async Task ZA033_a_user_erased_by_deleting_is_not_recovered()
    {
        await using var site = await SiteAsync("placeholder");
        var deleter = await site.CreateUserAsync("deleter");
        site.SignIn(deleter);
        var lost = await site.CreateUserAsync("lost");
        using (var setup = site.CreateScope())
        {
            var deleted = await setup.ServiceProvider.GetRequiredService<ZauberCMS.Core.Membership.Interfaces.IMembershipService>()
                .DeleteUserAsync(new ZauberCMS.Core.Membership.Parameters.DeleteUserParameters { Id = lost.Id },
                    TestContext.Current.CancellationToken);
            Assert.True(deleted.Success, TestSite.Describe(deleted.Messages));
        }

        // The key names what is left of the account
        site.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<ZauberCMS.Core.Settings.ZauberSettings>>()
            .Value.Identity.RecoverAdminUser = $"deleted-{lost.Id:N}";
        await InitialiseAsync(site);

        using var scope = site.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var erased = (await userManager.FindByIdAsync(lost.Id.ToString()))!;
        Assert.True(erased.Deleted);
        Assert.Empty(await userManager.GetRolesAsync(erased));
    }

    [Fact]
    public async Task A_name_nobody_has_creates_no_account()
    {
        await using var site = await SiteAsync("nobody@example.test");
        await site.CreateUserAsync("somebody");

        await InitialiseAsync(site);

        Assert.Equal(["somebody"], await site.QueryDatabaseAsync(db =>
            Task.FromResult(db.Users.Select(x => x.UserName!).ToList())));
        using var scope = site.CreateScope();
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<UserManager<User>>()
            .GetUsersInRoleAsync(Constants.Roles.AdminRoleName));
    }
}

/// <summary>Found by discovery and chosen with <c>Zauber:Plugins:EmailProvider</c>: remembers what was sent.</summary>
public class RecordingEmailProvider : ZauberCMS.Core.Plugins.Interfaces.IEmailProvider
{
    public static System.Collections.Concurrent.ConcurrentBag<(string To, string Subject, List<string> Paragraphs)> Sent { get; } = [];

    public Task SendEmailWithTemplateAsync(string toEmail, string subject, List<string> paragraphs)
    {
        Sent.Add((toEmail, subject, paragraphs));
        return Task.CompletedTask;
    }

    public Task SendEmailAsync(string toEmail, string subject, string message)
    {
        Sent.Add((toEmail, subject, [message]));
        return Task.CompletedTask;
    }
}
