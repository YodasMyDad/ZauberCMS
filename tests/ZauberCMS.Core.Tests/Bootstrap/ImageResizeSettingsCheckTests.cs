using ImageResize.Core.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ZauberCMS.Core.Settings;
using ZauberCMS.Core.Tests.Infrastructure;

namespace ZauberCMS.Core.Tests.Bootstrap;

/// <summary>
/// ZA-235 (Roadmap D102): a <c>Zauber:ImageResize</c> number ImageResize refuses stops the start in the Zauber settings
/// check, naming the setting, its value and what is valid (D48), before the migrations; it used to pass that check and
/// stop the start later in ImageResize's own validator, from <c>app.Run()</c>, with ImageResize's option name.
/// </summary>
public class ImageResizeSettingsCheckTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // The Zauber setting, and the same number on ImageResize's options as the configure step maps it
    public static TheoryData<string> Settings => [.. Mappings.Keys];

    private static readonly Dictionary<string, (Action<ImageResizeSettings, long> Zauber, Action<ImageResizeOptions, long> Mapped)>
        Mappings = new()
        {
            ["DefaultQuality"] = ((s, v) => s.DefaultQuality = (int)v, (o, v) => o.DefaultQuality = (int)v),
            ["PngCompressionLevel"] = ((s, v) => s.PngCompressionLevel = (int)v, (o, v) => o.PngCompressionLevel = (int)v),
            ["Cache:FolderSharding"] = ((s, v) => s.Cache.FolderSharding = (int)v, (o, v) => o.Cache.FolderSharding = (int)v),
            ["Cache:MaxCacheBytes"] = ((s, v) => s.Cache.MaxCacheBytes = v, (o, v) => o.Cache.MaxCacheBytes = v),
            ["ResponseCache:ClientCacheSeconds"] = ((s, v) => s.ResponseCache.ClientCacheSeconds = (int)v,
                (o, v) => o.ResponseCache.ClientCacheSeconds = (int)v)
        };

    [Theory]
    [InlineData("DefaultQuality", "0", "1 to 100")]
    [InlineData("DefaultQuality", "101", "1 to 100")]
    [InlineData("PngCompressionLevel", "-1", "0 to 9")]
    [InlineData("PngCompressionLevel", "10", "0 to 9")]
    [InlineData("Cache:FolderSharding", "-1", "0 or more")]
    [InlineData("Cache:MaxCacheBytes", "-1", "0 for no limit")]
    [InlineData("ResponseCache:ClientCacheSeconds", "-5", "0 or more seconds")]
    public async Task ZA235_an_image_resize_number_out_of_range_stops_the_start_naming_the_setting(string setting,
        string value, string valid)
    {
        await using var app = TestSite.BuildApp("", "", TestSite.CreateContentRoot(), validateContainer: false,
            new Dictionary<string, string?>
            {
                [$"Zauber:ImageResize:{setting}"] = value,
                ["urls"] = "http://127.0.0.1:0"
            });

        // What AddZauberCms(app) reads first, before the migrations
        var error = Assert.Throws<OptionsValidationException>(() =>
            app.Services.GetRequiredService<IOptions<ZauberSettings>>().Value);
        var failure = Assert.Single(error.Failures);
        Assert.StartsWith($"Zauber:ImageResize:{setting} is {value}, ", failure, StringComparison.Ordinal);
        Assert.Contains(valid, failure, StringComparison.Ordinal);

        // At the host's start the Zauber check is the one that fails, not a second copy from ImageResize
        var start = await Assert.ThrowsAsync<OptionsValidationException>(() => app.StartAsync(Ct));
        Assert.Equal(typeof(ZauberSettings), start.OptionsType);
    }

    [Fact]
    public async Task ZA235_the_shipped_image_resize_settings_and_the_edges_of_each_range_pass()
    {
        var edges = new Dictionary<string, string?>
        {
            ["Zauber:ImageResize:DefaultQuality"] = "1",
            ["Zauber:ImageResize:PngCompressionLevel"] = "9",
            ["Zauber:ImageResize:Cache:FolderSharding"] = "0",
            ["Zauber:ImageResize:Cache:MaxCacheBytes"] = "0",
            ["Zauber:ImageResize:ResponseCache:ClientCacheSeconds"] = "0"
        };
        await using var app = TestSite.BuildApp("", "", TestSite.CreateContentRoot(), validateContainer: false, edges);

        Assert.Empty(ZauberSettingsValidator.ImageResizeFailures(new ImageResizeSettings()));
        Assert.Equal(1, app.Services.GetRequiredService<IOptions<ZauberSettings>>().Value.ImageResize.DefaultQuality);
        Assert.Equal(1, app.Services.GetRequiredService<IOptions<ImageResizeOptions>>().Value.DefaultQuality);
    }

    [Theory]
    [MemberData(nameof(Settings))]
    public async Task ZA235_the_zauber_check_refuses_exactly_what_image_resizes_own_validator_refuses(string setting)
    {
        // ImageResize's validator is internal to it; it is registered with its options, so the site's container has it
        await using var app = TestSite.BuildApp("", "", TestSite.CreateContentRoot(), validateContainer: false);
        var imageResizeValidators = app.Services.GetServices<IValidateOptions<ImageResizeOptions>>()
            .Where(v => v.GetType().Assembly == typeof(ImageResizeOptions).Assembly)
            .ToList();
        Assert.NotEmpty(imageResizeValidators);
        var (zauber, mapped) = Mappings[setting];

        foreach (var value in new long[] { -2, -1, 0, 1, 2, 9, 10, 99, 100, 101, 604800 })
        {
            var settings = new ImageResizeSettings();
            zauber(settings, value);
            var options = new ImageResizeOptions();
            mapped(options, value);

            var zauberRefuses = ZauberSettingsValidator.ImageResizeFailures(settings).Any();
            var imageResizeRefuses = imageResizeValidators.Any(v => v.Validate(null, options).Failed);
            Assert.True(zauberRefuses == imageResizeRefuses,
                $"{setting} = {value}: the Zauber check {(zauberRefuses ? "refuses" : "accepts")} it, " +
                $"ImageResize {(imageResizeRefuses ? "refuses" : "accepts")} it");
        }
    }
}
