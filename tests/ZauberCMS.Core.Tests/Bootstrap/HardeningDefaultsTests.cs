using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ZauberCMS.Core.Settings;
using ZauberCMS.Core.Tests.Infrastructure;

namespace ZauberCMS.Core.Tests.Bootstrap;

/// <summary>Defaults and settings from Roadmap Phase 10e: image resizing (ZA-104, D36), rate limits (ZA-149), email (ZA-032).</summary>
public class HardeningDefaultsTests
{
    [Fact]
    public void ZA104_images_are_not_upscaled_and_the_resize_cache_is_capped_by_default()
    {
        var settings = new ZauberSettings();

        Assert.False(settings.ImageResize.AllowUpscale);
        Assert.Equal(2L * 1024 * 1024 * 1024, settings.ImageResize.Cache.MaxCacheBytes);
    }

    [Fact]
    public void ZA149_the_rate_limits_default_to_the_values_that_were_fixed_in_code()
    {
        var identity = new ZauberSettings().Identity;

        Assert.Equal((10, 5), (identity.LoginRateLimit.PermitLimit, identity.LoginRateLimit.WindowMinutes));
        Assert.Equal((5, 10), (identity.RegisterRateLimit.PermitLimit, identity.RegisterRateLimit.WindowMinutes));
        Assert.Equal(TimeSpan.FromMinutes(5), identity.LoginRateLimit.ToLimiterOptions().Window);
    }

    [Fact]
    public async Task ZA149_a_site_can_change_one_rate_limit_value_and_keep_the_other()
    {
        await using var site = await TestSite.CreateAsync(new Dictionary<string, string?>
        {
            ["Zauber:Identity:LoginRateLimit:PermitLimit"] = "25"
        }, createUser: false);

        var limit = site.Services.GetRequiredService<IOptions<ZauberSettings>>().Value.Identity.LoginRateLimit;

        Assert.Equal(25, limit.PermitLimit);
        Assert.Equal(5, limit.WindowMinutes);
    }

    [Theory]
    [InlineData("ZauberCMS/appsettings.json")]
    [InlineData("ZauberCMS.Web/appsettings.json")]
    [InlineData("ZauberCMS.Template/template/ZauberCMSTemplate.Site/appsettings.json")]
    public void ZA032_the_shipped_settings_hold_no_made_up_email_credentials(string file)
    {
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepositoryRoot(), file)),
            new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        var email = json.RootElement.GetProperty("Zauber").GetProperty("Email");
        var smtp = email.GetProperty("Smtp");

        Assert.Equal("", email.GetProperty("SenderEmail").GetString());
        Assert.Equal("", smtp.GetProperty("Host").GetString());
        Assert.Equal("", smtp.GetProperty("Username").GetString());
        Assert.Equal("", smtp.GetProperty("Password").GetString());
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "ZauberCMS.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("ZauberCMS.sln not found");
    }
}
