using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Data.Interfaces;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Membership;
using ZauberCMS.Core.Membership.Interfaces;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Membership.Parameters;
using ZauberCMS.Core.Membership.Services;
using ZauberCMS.Core.Shared.Models;
using ZauberCMS.Core.Tests.Infrastructure;

namespace ZauberCMS.Core.Tests.Membership;

/// <summary>
/// A user's own name, email address and password, as the admin's current-user panel changes them (Roadmap D72,
/// ZP-001): the current password guards the address and the password, a wrong one counts towards the lockout, the
/// address moves only once the new one is proven, and nothing else of the account changes.
/// </summary>
public partial class CurrentUserAccountTests : IAsyncLifetime
{
    private const string Password = "Passw0rd-For-Tests";

    // The English of membership_lockedWrongPasswords (Core's Membership.resx), which the tests' culture shows
    private const string LockedOutMessage =
        "Too many wrong passwords: the account is locked for a few minutes. Try again later.";

    /// <summary>Overridden by the PostgreSQL and SQL Server copies of this class at the end of the file.</summary>
    protected virtual string DatabaseProvider => TestSite.SqliteProvider;

    private TestSite _site = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => _site = await TestSite.CreateAsync(new Dictionary<string, string?>
    {
        ["Zauber:Plugins:EmailProvider"] = typeof(RecordingEmailProvider).FullName,
        ["Zauber:SiteUrl"] = "https://site.test"
    }, DatabaseProvider);

    public async ValueTask DisposeAsync() => await _site.DisposeAsync();

    // ---- Name ----

    [Fact]
    public async Task D72_the_name_changes_and_nothing_else_does_and_no_session_ends()
    {
        var user = await CreateSignedInEditorAsync("renamer");
        var before = await StoredAsync(user.Id);

        var result = await Membership(s => s.ChangeUserNameAsync(new ChangeUserNameParameters { UserId = user.Id, Name = "  Renamed Person " }, Ct));

        Assert.True(result.Success, TestSite.Describe(result.Messages));
        var after = await StoredAsync(user.Id);
        Assert.Equal("Renamed Person", after.UserName);
        Assert.Equal(before.Email, after.Email);
        Assert.Equal(before.SecurityStamp, after.SecurityStamp);
        Assert.Equal(before.PasswordHash, after.PasswordHash);
        Assert.Equal([Constants.Roles.EditorRoleName], await RolesAsync(user.Id));
        Assert.False(result.RefreshSignIn);
        Assert.True(await AuditedAsync("renamer changed their name from renamer to Renamed Person"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("taken")]
    public async Task D72_an_empty_or_taken_name_is_refused(string name)
    {
        await _site.CreateUserAsync("taken");
        var user = await CreateSignedInEditorAsync("keeper");

        var result = await Membership(s => s.ChangeUserNameAsync(new ChangeUserNameParameters { UserId = user.Id, Name = name }, Ct));

        Assert.False(result.Success);
        Assert.NotEmpty(result.Messages);
        Assert.Equal("keeper", (await StoredAsync(user.Id)).UserName);
    }

    // ---- Password ----

    [Fact]
    public async Task D72_the_password_changes_with_the_current_one_and_only_the_callers_sign_in_is_renewed()
    {
        var user = await CreateSignedInEditorAsync("changer");
        var before = await StoredAsync(user.Id);

        var result = await ChangePasswordAsync(user, Password, "N3w-Passw0rd-Chosen");

        Assert.True(result.Success, TestSite.Describe(result.Messages));
        var after = await StoredAsync(user.Id);
        // A new stamp ends the user's other sessions; the caller's own is renewed with the ticket
        Assert.NotEqual(before.SecurityStamp, after.SecurityStamp);
        Assert.True(result.RefreshSignIn);
        Assert.True(_site.Services.GetRequiredService<RefreshSignInTicketService>().IsValid(result.RefreshSignInTicket, after));
        Assert.True(await CheckPasswordAsync(user.Id, "N3w-Passw0rd-Chosen"));
        Assert.False(await CheckPasswordAsync(user.Id, Password));
        Assert.Equal([Constants.Roles.EditorRoleName], await RolesAsync(user.Id));
        Assert.True(await AuditedAsync("changer changed their password"));
    }

    [Fact]
    public async Task D72_changing_someone_elses_password_renews_no_sign_in()
    {
        var other = await _site.CreateUserAsync("other");
        _site.SignIn(_site.CurrentUser);

        var result = await ChangePasswordAsync(other, Password, "N3w-Passw0rd-Chosen");

        Assert.True(result.Success, TestSite.Describe(result.Messages));
        Assert.False(result.RefreshSignIn);
        Assert.Null(result.RefreshSignInTicket);
        Assert.True(await AuditedAsync("test-admin changed the password of other"));
    }

    [Fact]
    public async Task D72_a_wrong_current_password_is_refused_and_six_lock_the_account()
    {
        var user = await CreateSignedInEditorAsync("guesser");

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            var wrong = await ChangePasswordAsync(user, $"Wrong-{attempt}", "N3w-Passw0rd-Chosen");
            Assert.False(wrong.Success);
            Assert.Equal("The current password is not right.", TestSite.Describe(wrong.Messages));
        }

        var sixth = await ChangePasswordAsync(user, "Wrong-6", "N3w-Passw0rd-Chosen");
        Assert.Equal(LockedOutMessage, TestSite.Describe(sixth.Messages));

        // Locked: even the right password changes nothing until the lockout ends
        var right = await ChangePasswordAsync(user, Password, "N3w-Passw0rd-Chosen");
        Assert.False(right.Success);
        Assert.Equal(LockedOutMessage, TestSite.Describe(right.Messages));
        Assert.True(await CheckPasswordAsync(user.Id, Password));
    }

    [Fact]
    public async Task D72_a_right_current_password_clears_the_wrong_ones_before_it()
    {
        var user = await CreateSignedInEditorAsync("forgetful");
        await ChangePasswordAsync(user, "Wrong-1", "N3w-Passw0rd-Chosen");

        Assert.True((await ChangePasswordAsync(user, Password, "N3w-Passw0rd-Chosen")).Success);

        Assert.Equal(0, (await StoredAsync(user.Id)).AccessFailedCount);
    }

    [Theory]
    [InlineData("short")]
    [InlineData("alllowercase1")]
    [InlineData("")]
    public async Task D72_a_new_password_that_breaks_the_rules_is_refused(string newPassword)
    {
        var user = await CreateSignedInEditorAsync("ruler");
        var before = await StoredAsync(user.Id);

        var result = await ChangePasswordAsync(user, Password, newPassword);

        Assert.False(result.Success);
        Assert.NotEmpty(result.Messages);
        Assert.True(await CheckPasswordAsync(user.Id, Password));
        Assert.Equal(before.SecurityStamp, (await StoredAsync(user.Id)).SecurityStamp);
    }

    [Fact]
    public async Task D72_an_account_without_a_password_cannot_change_its_password_or_address_here()
    {
        var user = await CreateSignedInEditorAsync("external");
        await WithUserManagerAsync(async manager =>
            Assert.True((await manager.RemovePasswordAsync((await manager.FindByIdAsync(user.Id.ToString()))!)).Succeeded));

        var password = await ChangePasswordAsync(user, Password, "N3w-Passw0rd-Chosen");
        var email = await RequestEmailChangeAsync(user, "external-new@example.test", Password);

        Assert.Contains("no password", TestSite.Describe(password.Messages));
        Assert.Contains("no password", TestSite.Describe(email.Messages));
    }

    // ---- Email address ----

    [Fact]
    public async Task D72_the_address_moves_only_when_the_link_sent_to_the_new_one_is_opened()
    {
        var user = await CreateSignedInEditorAsync("mover");
        var newEmail = $"mover-{Guid.NewGuid():N}@example.test";
        var before = await StoredAsync(user.Id);

        var result = await RequestEmailChangeAsync(user, newEmail, Password);

        Assert.True(result.Success, TestSite.Describe(result.Messages));
        Assert.Equal(before.Email, (await StoredAsync(user.Id)).Email);
        var mail = Assert.Single(RecordingEmailProvider.Sent, m => m.To == newEmail);
        Assert.Equal("Confirm your new email address", mail.Subject);
        Assert.DoesNotContain(RecordingEmailProvider.Sent, m => m.To == before.Email);
        Assert.True(await AuditedAsync("mover asked to change their email address"));

        var link = new Uri(WebUtility.HtmlDecode(LinkPattern().Match(string.Join(" ", mail.Paragraphs)).Groups[1].Value));
        Assert.StartsWith("https://site.test/account/confirmemail", link.GetLeftPart(UriPartial.Path), StringComparison.OrdinalIgnoreCase);
        var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(link.Query);
        var confirmed = await Membership(s => s.ConfirmEmailAsync(new ConfirmEmailParameters
        {
            UserId = query["userId"], Code = query["code"], IsEmailUpdate = query["change"] == "true", NewEmailAddress = query["email"]
        }, Ct));

        Assert.True(confirmed.Success, TestSite.Describe(confirmed.Messages));
        var after = await StoredAsync(user.Id);
        Assert.Equal(newEmail, after.Email);
        Assert.True(after.EmailConfirmed);
        Assert.NotEqual(before.SecurityStamp, after.SecurityStamp);
        Assert.Equal([Constants.Roles.EditorRoleName], await RolesAsync(user.Id));
        Assert.True(await AuditedAsync("mover changed their email address"));
    }

    [Fact]
    public async Task D72_only_the_newest_link_moves_the_account()
    {
        // A mistyped address, then the right one: the first link must not let its reader take the account
        var user = await CreateSignedInEditorAsync("corrector");
        var mistyped = $"corector-{Guid.NewGuid():N}@example.test";
        var right = $"corrector-{Guid.NewGuid():N}@example.test";
        Assert.True((await RequestEmailChangeAsync(user, mistyped, Password)).Success);
        ForgetLinkSent(user);
        Assert.True((await RequestEmailChangeAsync(user, right, Password)).Success);

        var older = await ConfirmFromMailAsync(mistyped);
        var newest = await ConfirmFromMailAsync(right);

        Assert.False(older.Success);
        Assert.Contains("newer link", TestSite.Describe(older.Messages));
        Assert.True(newest.Success, TestSite.Describe(newest.Messages));
        Assert.Equal(right, (await StoredAsync(user.Id)).Email);
    }

    [Fact]
    public async Task D72_an_address_another_account_has_is_refused_when_its_link_is_opened_not_before()
    {
        // A name of its own: the recorded mail is shared by the three providers' copies of these tests
        var owner = await _site.CreateUserAsync($"owner-{Guid.NewGuid():N}");
        var user = await CreateSignedInEditorAsync("copier");

        // Nothing here says the address has an account
        var request = await RequestEmailChangeAsync(user, owner.Email!, Password);
        Assert.True(request.Success, TestSite.Describe(request.Messages));

        var confirmed = await ConfirmFromMailAsync(owner.Email!);
        Assert.False(confirmed.Success);
        Assert.Equal("copier@example.test", (await StoredAsync(user.Id)).Email);
    }

    [Fact]
    public async Task D72_the_link_is_built_from_the_site_address_when_there_is_no_request()
    {
        // An admin circuit on Long Polling or Azure SignalR has no request to take the host from
        var user = await CreateSignedInEditorAsync("circuit");
        var newEmail = $"circuit-{Guid.NewGuid():N}@example.test";

        var result = await Membership(s => s.RequestEmailChangeAsync(new RequestEmailChangeParameters
        {
            UserId = user.Id, NewEmailAddress = newEmail, CurrentPassword = Password
        }, Ct), withRequest: false);

        Assert.True(result.Success, TestSite.Describe(result.Messages));
        var mail = Assert.Single(RecordingEmailProvider.Sent, m => m.To == newEmail);
        Assert.Contains("href='https://site.test/account/confirmemail?", string.Join(" ", mail.Paragraphs),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task D72_a_name_longer_than_the_table_keeps_is_refused()
    {
        var user = await CreateSignedInEditorAsync("short");

        var result = await Membership(s => s.ChangeUserNameAsync(new ChangeUserNameParameters
        {
            UserId = user.Id, Name = new string('n', MembershipService.MaxUserNameLength + 1)
        }, Ct));

        Assert.False(result.Success);
        Assert.Equal("short", (await StoredAsync(user.Id)).UserName);
    }

    private async Task<AuthenticationResult> ConfirmFromMailAsync(string address)
    {
        var mail = Assert.Single(RecordingEmailProvider.Sent, m => m.To == address);
        var link = new Uri(WebUtility.HtmlDecode(LinkPattern().Match(string.Join(" ", mail.Paragraphs)).Groups[1].Value));
        var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(link.Query);
        return await Membership(s => s.ConfirmEmailAsync(new ConfirmEmailParameters
        {
            UserId = query["userId"], Code = query["code"], IsEmailUpdate = query["change"] == "true", NewEmailAddress = query["email"]
        }, Ct));
    }

    private void ForgetLinkSent(User user) =>
        _site.Services.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>()
            .Remove($"ZauberCMS.EmailChangeLinkSent.{user.Id}");

    [Fact]
    public async Task D72_an_address_change_needs_the_right_current_password_and_counts_a_wrong_one()
    {
        var user = await CreateSignedInEditorAsync("sneaky");
        var newEmail = $"sneaky-{Guid.NewGuid():N}@example.test";

        var withoutPassword = await RequestEmailChangeAsync(user, newEmail, null);
        var wrongPassword = await RequestEmailChangeAsync(user, newEmail, "Wrong-1");

        Assert.Equal("Enter your current password.", TestSite.Describe(withoutPassword.Messages));
        Assert.Equal("The current password is not right.", TestSite.Describe(wrongPassword.Messages));
        Assert.DoesNotContain(RecordingEmailProvider.Sent, m => m.To == newEmail);
        Assert.Equal(1, (await StoredAsync(user.Id)).AccessFailedCount);
    }

    [Theory]
    [InlineData("not-an-address")]
    [InlineData("")]
    [InlineData("SAME")]
    public async Task D72_a_wrong_or_unchanged_address_is_refused(string newEmail)
    {
        var user = await CreateSignedInEditorAsync("stayer");
        if (newEmail == "SAME")
        {
            newEmail = "STAYER@example.test";
        }

        var result = await RequestEmailChangeAsync(user, newEmail, Password);

        Assert.False(result.Success);
        Assert.NotEmpty(result.Messages);
        Assert.DoesNotContain(RecordingEmailProvider.Sent, m => m.To == newEmail);
    }

    [Fact]
    public async Task D72_one_link_a_minute_per_account()
    {
        var user = await CreateSignedInEditorAsync("hasty");
        var first = $"hasty-{Guid.NewGuid():N}@example.test";
        var second = $"hasty-{Guid.NewGuid():N}@example.test";

        Assert.True((await RequestEmailChangeAsync(user, first, Password)).Success);
        var again = await RequestEmailChangeAsync(user, second, Password);

        Assert.False(again.Success);
        Assert.Contains("less than a minute ago", TestSite.Describe(again.Messages));
        Assert.DoesNotContain(RecordingEmailProvider.Sent, m => m.To == second);
    }

    [Fact]
    public async Task D72_moving_to_an_address_on_the_admin_list_does_not_make_an_administrator()
    {
        // The link proves that somebody opened the email, not that the account is theirs (D22)
        var listed = $"boss-{Guid.NewGuid():N}@example.test";
        using (var scope = _site.CreateScope())
        {
            var dataService = scope.ServiceProvider.GetRequiredService<IDataService>();
            var settings = await dataService.GetGlobalSettings();
            settings.AdminEmailAddresses = [listed];
            Assert.True(await dataService.SaveGlobalSettings(settings));
        }

        var user = await CreateSignedInEditorAsync("climber");
        Assert.True((await RequestEmailChangeAsync(user, listed, Password)).Success);
        var mail = Assert.Single(RecordingEmailProvider.Sent, m => m.To == listed);
        var link = new Uri(WebUtility.HtmlDecode(LinkPattern().Match(string.Join(" ", mail.Paragraphs)).Groups[1].Value));
        var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(link.Query);
        Assert.True((await Membership(s => s.ConfirmEmailAsync(new ConfirmEmailParameters
        {
            UserId = query["userId"], Code = query["code"], IsEmailUpdate = true, NewEmailAddress = query["email"]
        }, Ct))).Success);

        Assert.Equal(listed, (await StoredAsync(user.Id)).Email);
        Assert.Equal([Constants.Roles.EditorRoleName], await RolesAsync(user.Id));
    }

    [GeneratedRegex("href='([^']+)'")]
    private static partial Regex LinkPattern();

    // ---- Helpers ----

    private async Task<User> CreateSignedInEditorAsync(string name)
    {
        var user = await _site.CreateUserAsync(name);
        await WithUserManagerAsync(async manager =>
            Assert.True((await manager.AddToRoleAsync((await manager.FindByIdAsync(user.Id.ToString()))!,
                Constants.Roles.EditorRoleName)).Succeeded));
        _site.SignIn(user);
        return user;
    }

    private Task<HandlerResult<User>> ChangePasswordAsync(User user, string? current, string newPassword) =>
        Membership(s => s.ChangePasswordAsync(new ChangePasswordParameters
        {
            UserId = user.Id, CurrentPassword = current, NewPassword = newPassword
        }, Ct));

    private Task<HandlerResult<User>> RequestEmailChangeAsync(User user, string newEmail, string? current) =>
        Membership(s => s.RequestEmailChangeAsync(new RequestEmailChangeParameters
        {
            UserId = user.Id, NewEmailAddress = newEmail, CurrentPassword = current
        }, Ct));

    private async Task<T> Membership<T>(Func<IMembershipService, Task<T>> call, bool withRequest = true)
    {
        using var scope = _site.CreateScope();
        // The emailed links are built for the request's site, or Zauber:SiteUrl without one
        scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = withRequest
            ? new DefaultHttpContext { RequestServices = scope.ServiceProvider }
            : null;
        return await call(scope.ServiceProvider.GetRequiredService<IMembershipService>());
    }

    private Task<User> StoredAsync(Guid userId) =>
        _site.QueryDatabaseAsync(db => db.Users.AsNoTracking().SingleAsync(x => x.Id == userId, Ct));

    private Task<List<string>> RolesAsync(Guid userId) =>
        _site.QueryDatabaseAsync(db => db.UserRoles.AsNoTracking().Where(x => x.UserId == userId)
            .Select(x => x.Role.Name!).ToListAsync(Ct));

    private Task<bool> AuditedAsync(string description) =>
        _site.QueryDatabaseAsync(db => db.Audits.AnyAsync(x => x.Description == description, Ct));

    private async Task<bool> CheckPasswordAsync(Guid userId, string password)
    {
        var ok = false;
        await WithUserManagerAsync(async manager =>
            ok = await manager.CheckPasswordAsync((await manager.FindByIdAsync(userId.ToString()))!, password));
        return ok;
    }

    private async Task WithUserManagerAsync(Func<UserManager<User>, Task> action)
    {
        using var scope = _site.CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<UserManager<User>>());
    }
}

/// <summary>The same tests on a real PostgreSQL (Roadmap D15); skipped without Docker.</summary>
public class CurrentUserAccountTestsOnPostgreSql : CurrentUserAccountTests
{
    protected override string DatabaseProvider => TestSite.PostgreSqlProvider;
}

/// <summary>The same tests on SQL Server; skipped without <c>ZAUBER_TEST_SQLSERVER</c>.</summary>
public class CurrentUserAccountTestsOnSqlServer : CurrentUserAccountTests
{
    protected override string DatabaseProvider => TestSite.SqlServerProvider;
}
