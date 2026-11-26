using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using ZauberCMS.Core.Plugins;
using ZauberCMS.Core.Providers;
using ZauberCMS.Core.Security.Interfaces;
using ZauberCMS.Core.Security.Models;
using ZauberCMS.Core.Security.Services;
using ZauberCMS.Core.Settings;
using ZauberCMS.Core.Tests.Infrastructure;

namespace ZauberCMS.Core.Tests.Security;

/// <summary>
/// ZA-024 / D23: the registration form refuses posts that a script made (the field people cannot see, and a
/// form that comes back faster than a person fills it in), and a site can add a CAPTCHA by discovery.
/// </summary>
public class FormProtectionTests
{
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 11, 26, 9, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static (FormProtectionService Service, Clock Clock) Create(int minimumSeconds = 3)
    {
        var clock = new Clock();
        var settings = new ZauberSettings { FormProtection = { MinimumFillSeconds = minimumSeconds } };
        return (new FormProtectionService(new EphemeralDataProtectionProvider(), Options.Create(settings), clock), clock);
    }

    [Fact]
    public void A_form_sent_after_a_human_amount_of_time_passes()
    {
        var (service, clock) = Create();
        var token = service.CreateToken();

        clock.Now += TimeSpan.FromSeconds(8);

        Assert.Equal(FormProtectionResult.Passed, service.Validate(token, ""));
        Assert.Equal(FormProtectionResult.Passed, service.Validate(token, null));
    }

    [Fact]
    public void A_form_that_comes_straight_back_is_too_fast()
    {
        var (service, clock) = Create();
        var token = service.CreateToken();

        clock.Now += TimeSpan.FromSeconds(1);
        Assert.Equal(FormProtectionResult.TooFast, service.Validate(token, ""));

        // The same token is fine once enough time has passed: a quick person only has to send it again
        clock.Now += TimeSpan.FromSeconds(3);
        Assert.Equal(FormProtectionResult.Passed, service.Validate(token, ""));
    }

    [Fact]
    public void The_time_check_can_be_turned_off()
    {
        var (service, _) = Create(minimumSeconds: 0);

        Assert.Equal(FormProtectionResult.Passed, service.Validate(service.CreateToken(), ""));
    }

    [Fact]
    public void A_filled_in_hidden_field_is_a_script_however_long_it_waited()
    {
        var (service, clock) = Create();
        var token = service.CreateToken();
        clock.Now += TimeSpan.FromMinutes(1);

        Assert.Equal(FormProtectionResult.HoneypotFilled, service.Validate(token, "https://spam.example"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-token")]
    public void A_missing_or_made_up_token_is_refused(string? token)
    {
        var (service, _) = Create();

        Assert.Equal(FormProtectionResult.Expired, service.Validate(token, ""));
    }

    [Fact]
    public void A_token_from_another_site_or_one_left_open_overnight_is_refused()
    {
        var (service, clock) = Create();
        var (otherSite, _) = Create();
        var token = service.CreateToken();

        Assert.Equal(FormProtectionResult.Expired, service.Validate(otherSite.CreateToken(), ""));

        clock.Now += FormProtectionService.MaximumAge + TimeSpan.FromMinutes(1);
        Assert.Equal(FormProtectionResult.Expired, service.Validate(token, ""));
    }

    [Fact]
    public void The_posted_form_is_read_by_field_name()
    {
        var (service, clock) = Create();
        var token = service.CreateToken();
        clock.Now += TimeSpan.FromSeconds(10);

        HttpContext Post(string honeypot)
        {
            var context = new DefaultHttpContext();
            context.Request.Method = "POST";
            context.Request.ContentType = "application/x-www-form-urlencoded";
            context.Features.Set<IFormFeature>(new FormFeature(new FormCollection(new Dictionary<string, StringValues>
            {
                [service.TokenFieldName] = token,
                [service.HoneypotFieldName] = honeypot
            })));
            return context;
        }

        Assert.Equal(FormProtectionResult.Passed, service.Validate(Post("")));
        Assert.Equal(FormProtectionResult.HoneypotFilled, service.Validate(Post("x")));
        Assert.Equal(FormProtectionResult.Expired, service.Validate(new DefaultHttpContext()));
    }

    [Fact]
    public async Task The_site_resolves_the_service_and_finds_a_captcha_provider_by_discovery()
    {
        await using var site = await TestSite.CreateAsync();
        using var scope = site.CreateScope();

        Assert.IsType<FormProtectionService>(scope.ServiceProvider.GetRequiredService<IFormProtectionService>());

        // Two providers are in the test assembly; with no key the first by type name is used
        var provider = scope.ServiceProvider.GetRequiredService<ProviderService>().CaptchaProvider;
        Assert.IsType<AlwaysPassCaptcha>(provider);
        Assert.Contains(typeof(NeverPassCaptcha).FullName!,
            scope.ServiceProvider.GetRequiredService<ExtensionManager>().GetImplementations<ICaptchaProvider>().Keys);
    }

    [Fact]
    public async Task The_configured_captcha_provider_wins_and_a_wrong_name_is_an_error()
    {
        await using (var site = await TestSite.CreateAsync(new Dictionary<string, string?>
                     {
                         ["Zauber:Plugins:CaptchaProvider"] = typeof(NeverPassCaptcha).FullName
                     }))
        {
            using var scope = site.CreateScope();
            var provider = scope.ServiceProvider.GetRequiredService<ProviderService>().CaptchaProvider;
            Assert.IsType<NeverPassCaptcha>(provider);
            Assert.False(await provider!.VerifyAsync(new DefaultHttpContext(), TestContext.Current.CancellationToken));
        }

        await using var misconfigured = await TestSite.CreateAsync(new Dictionary<string, string?>
        {
            ["Zauber:Plugins:CaptchaProvider"] = "Some.Missing.Captcha"
        });
        using var badScope = misconfigured.CreateScope();
        Assert.Throws<InvalidOperationException>(() =>
            badScope.ServiceProvider.GetRequiredService<ProviderService>().CaptchaProvider);
    }
}

/// <summary>Found by discovery, like a CAPTCHA in a site's project.</summary>
public class AlwaysPassCaptcha : ICaptchaProvider
{
    public Type Component => typeof(Microsoft.AspNetCore.Components.Sections.SectionOutlet);

    public Task<bool> VerifyAsync(HttpContext httpContext, CancellationToken cancellationToken = default) =>
        Task.FromResult(true);
}

public class NeverPassCaptcha : ICaptchaProvider
{
    public Type Component => typeof(Microsoft.AspNetCore.Components.Sections.SectionOutlet);

    public Task<bool> VerifyAsync(HttpContext httpContext, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);
}
