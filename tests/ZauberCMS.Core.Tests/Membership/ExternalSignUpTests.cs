using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.Facebook;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.MicrosoftAccount;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ZauberCMS.Core.Membership;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Tests.Infrastructure;

namespace ZauberCMS.Core.Tests.Membership;

/// <summary>
/// A first sign-in through an external provider creates a local account only when the provider states that the
/// email address is verified, and each built-in provider must be able to supply that statement (ZA-002).
/// These tests stop at the claims: no request is made to a real provider.
/// </summary>
public class ExternalSignUpTests : IAsyncLifetime
{
    private TestSite _site = null!;

    public async ValueTask InitializeAsync() => _site = await TestSite.CreateAsync(new Dictionary<string, string?>
    {
        ["Zauber:Identity:ExternalProviders:Google:ClientId"] = "google-id",
        ["Zauber:Identity:ExternalProviders:Google:ClientSecret"] = "google-secret",
        ["Zauber:Identity:ExternalProviders:Facebook:AppId"] = "facebook-id",
        ["Zauber:Identity:ExternalProviders:Facebook:AppSecret"] = "facebook-secret",
        ["Zauber:Identity:ExternalProviders:Facebook:TrustEmail"] = "true",
        ["Zauber:Identity:ExternalProviders:Microsoft:ClientId"] = "microsoft-id",
        ["Zauber:Identity:ExternalProviders:Microsoft:ClientSecret"] = "microsoft-secret"
    });

    public async ValueTask DisposeAsync() => await _site.DisposeAsync();

    [Fact]
    public async Task A_verified_email_creates_a_confirmed_user_with_the_login_and_the_starting_role()
    {
        using var scope = _site.CreateScope();
        var signInManager = SignInManager(scope);

        var user = await signInManager.CreateExternalUserAsync(
            Login("Google", "google-key-1", "ada@example.test", emailVerified: "True"));

        Assert.NotNull(user);
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var stored = await userManager.FindByLoginAsync("Google", "google-key-1");
        Assert.NotNull(stored);
        Assert.Equal("ada@example.test", stored.Email);
        Assert.True(stored.EmailConfirmed);
        Assert.Equal(["Member"], await userManager.GetRolesAsync(stored));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("false")]
    [InlineData("")]
    public async Task An_email_the_provider_did_not_verify_creates_no_user(string? emailVerified)
    {
        using var scope = _site.CreateScope();

        var user = await SignInManager(scope).CreateExternalUserAsync(
            Login("Google", "google-key-2", "mallory@example.test", emailVerified));

        Assert.Null(user);
        Assert.False(await _site.QueryDatabaseAsync(db => db.Users.AnyAsync(x => x.Email == "mallory@example.test")));
    }

    // ZA-024 / D23: a first sign-in through a provider is a registration
    [Fact]
    public async Task With_registration_turned_off_a_first_external_sign_in_creates_no_user()
    {
        using var scope = _site.CreateScope();
        var dataService = scope.ServiceProvider.GetRequiredService<ZauberCMS.Core.Data.Interfaces.IDataService>();
        var settings = await ZauberCMS.Core.Extensions.ServiceExtensions.GetGlobalSettings(dataService);
        settings.AllowRegistration = false;
        Assert.True(await ZauberCMS.Core.Extensions.ServiceExtensions.SaveGlobalSettings(dataService, settings));

        var user = await SignInManager(scope).CreateExternalUserAsync(
            Login("Google", "google-key-closed", "grace@example.test", emailVerified: "True"));

        Assert.Null(user);
        Assert.False(await _site.QueryDatabaseAsync(db => db.Users.AnyAsync(x => x.Email == "grace@example.test")));
    }

    [Fact]
    public async Task A_login_without_an_email_creates_no_user()
    {
        using var scope = _site.CreateScope();
        var usersBefore = await _site.QueryDatabaseAsync(db => db.Users.CountAsync());

        var user = await SignInManager(scope).CreateExternalUserAsync(
            Login("Google", "google-key-3", email: null, emailVerified: "true"));

        Assert.Null(user);
        Assert.Equal(usersBefore, await _site.QueryDatabaseAsync(db => db.Users.CountAsync()));
    }

    [Theory]
    [InlineData("""{"sub":"1","email":"ada@example.test","email_verified":true}""", true)]
    [InlineData("""{"sub":"1","email":"ada@example.test","email_verified":false}""", false)]
    [InlineData("""{"sub":"1","email":"ada@example.test"}""", false)]
    public void Google_passes_on_what_it_says_about_the_email(string userInfo, bool expectVerified)
    {
        var principal = ClaimsFrom<GoogleOptions>(GoogleDefaults.AuthenticationScheme, userInfo);

        Assert.Equal(expectVerified, ZauberSignInManager.HasVerifiedEmail(principal));
    }

    [Fact]
    public void Facebook_email_counts_as_verified_only_when_TrustEmail_is_set()
    {
        // TrustEmail is true for Facebook in this site's settings and not set for Microsoft.
        const string userInfo = """{"id":"1","email":"ada@example.test","mail":"ada@example.test"}""";

        Assert.True(ZauberSignInManager.HasVerifiedEmail(
            ClaimsFrom<FacebookOptions>(FacebookDefaults.AuthenticationScheme, userInfo)));
        Assert.False(ZauberSignInManager.HasVerifiedEmail(
            ClaimsFrom<MicrosoftAccountOptions>(MicrosoftAccountDefaults.AuthenticationScheme, userInfo)));
    }

    private static ZauberSignInManager SignInManager(IServiceScope scope) =>
        Assert.IsType<ZauberSignInManager>(scope.ServiceProvider.GetRequiredService<SignInManager<User>>());

    private static ExternalLoginInfo Login(string provider, string key, string? email, string? emailVerified)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, key), new(ClaimTypes.Name, $"user {key}") };
        if (email != null)
        {
            claims.Add(new Claim(ClaimTypes.Email, email));
        }

        if (emailVerified != null)
        {
            claims.Add(new Claim(ZauberSignInManager.EmailVerifiedClaimType, emailVerified));
        }

        return new ExternalLoginInfo(new ClaimsPrincipal(new ClaimsIdentity(claims, provider)), provider, key, provider);
    }

    /// <summary>Runs the provider's configured claim mapping over a user-info response, as its handler does.</summary>
    private ClaimsPrincipal ClaimsFrom<TOptions>(string scheme, string userInfoJson) where TOptions : OAuthOptions
    {
        var options = _site.Services.GetRequiredService<IOptionsMonitor<TOptions>>().Get(scheme);
        var identity = new ClaimsIdentity(scheme);
        using var document = JsonDocument.Parse(userInfoJson);
        foreach (var action in options.ClaimActions)
        {
            action.Run(document.RootElement, identity, scheme);
        }

        return new ClaimsPrincipal(identity);
    }
}
