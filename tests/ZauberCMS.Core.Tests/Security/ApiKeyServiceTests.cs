using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Data.Interfaces;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Settings.Interfaces;
using ZauberCMS.Core.Tests.Infrastructure;

namespace ZauberCMS.Core.Tests.Security;

/// <summary>
/// API keys by name, from configuration (<c>Zauber:ApiKeys</c>) and Global Settings, read on the server where they
/// are used (ZA-214, ZA-216, Roadmap D73).
/// </summary>
public class ApiKeyServiceTests : IAsyncLifetime
{
    private TestSite _site = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _site = await TestSite.CreateAsync(new Dictionary<string, string?>
        {
            // As the shipped appsettings.json has it: an empty TinyMCE entry
            ["Zauber:ApiKeys:TinyMCE"] = "",
            ["Zauber:ApiKeys:GoogleMaps"] = "maps-from-configuration",
            ["Zauber:ApiKeys:Stripe"] = "stripe-from-configuration"
        });

        using var scope = _site.CreateScope();
        var dataService = scope.ServiceProvider.GetRequiredService<IDataService>();
        var settings = await dataService.GetGlobalSettings();
        settings.ApiKeys = new Dictionary<string, string>
        {
            ["TinyMCE"] = "tiny-from-global-settings",
            ["googlemaps"] = "maps-from-global-settings",
            ["Empty"] = ""
        };
        Assert.True(await dataService.SaveGlobalSettings(settings));
    }

    public async ValueTask DisposeAsync() => await _site.DisposeAsync();

    [Fact]
    public async Task ZA216_keys_in_configuration_are_read_and_win_and_an_empty_one_hides_nothing()
    {
        var keys = Service();

        Assert.Equal(["GoogleMaps", "Stripe", "TinyMCE"], await keys.GetApiKeyNamesAsync(Ct));
        Assert.Equal("maps-from-configuration", await keys.GetApiKeyAsync("GoogleMaps", Ct));
        Assert.Equal("stripe-from-configuration", await keys.GetApiKeyAsync("stripe", Ct));
        Assert.Equal("tiny-from-global-settings", await keys.GetApiKeyAsync("TinyMCE", Ct));
        Assert.Null(await keys.GetApiKeyAsync("Empty", Ct));
        Assert.Null(await keys.GetApiKeyAsync("Unknown", Ct));
        Assert.Null(await keys.GetApiKeyAsync(" ", Ct));
    }

    [Theory]
    [InlineData("TinyMCE", "tiny-from-global-settings")]
    [InlineData("an-old-key-value", "an-old-key-value")]
    [InlineData("", null)]
    [InlineData(null, null)]
    public async Task ZA214_a_picked_name_gives_its_key_and_a_value_saved_before_v5_is_kept(string? picked,
        string? expected)
    {
        Assert.Equal(expected, await Service().GetPickedApiKeyAsync(picked, Ct));
    }

    private IApiKeyService Service() => _site.CreateScope().ServiceProvider.GetRequiredService<IApiKeyService>();
}
