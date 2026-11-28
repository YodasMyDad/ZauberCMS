using System.Net;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using ZauberCMS.Components.Admin.SettingsSection;
using ZauberCMS.Components.Editors;
using ZauberCMS.Components.Tests.Shared;
using ZauberCMS.Core.Localization;
using ZauberCMS.Core.Settings.Interfaces;

namespace ZauberCMS.Components.Tests.Editors;

/// <summary>
/// API keys are referred to by name; their values stay on the server and are never shown in the admin once saved
/// (ZA-214, Roadmap D73).
/// </summary>
public class ApiKeysPickerTests
{
    private const string Secret = "sk-live-0123456789";

    private static readonly Dictionary<string, string> Keys = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Maps"] = Secret,
        ["TinyMCE"] = "tiny-key"
    };

    // ZauberCMS's English text, as the Global Settings page passes its L (Roadmap D90)
    private static readonly ServiceProvider TextServices = CreateTextServices();
    private static IStringLocalizer<ZauberText> Text => TextServices.GetRequiredService<IStringLocalizer<ZauberText>>();

    [Fact]
    public async Task ZA214_a_value_saved_before_v5_is_shown_as_its_key_and_replaced_by_the_name()
    {
        var changes = new List<string>();

        var html = await RenderPickerAsync(Secret, changes);

        Assert.Equal(["Maps"], changes);
        Assert.DoesNotContain(Secret, html);
    }

    [Fact]
    public async Task ZA214_a_stored_name_is_kept_and_no_value_reaches_the_page()
    {
        var changes = new List<string>();

        var html = await RenderPickerAsync("Maps", changes);

        Assert.Empty(changes);
        Assert.DoesNotContain(Secret, html);
        Assert.DoesNotContain("tiny-key", html);
    }

    [Fact]
    public async Task ZA214_a_saved_value_that_no_key_has_now_is_left_as_it_is()
    {
        var changes = new List<string>();

        var html = await RenderPickerAsync("an-old-rotated-key", changes);

        Assert.Empty(changes);
        // The text is written encoded (site&#x27;s) now that it comes from the resources
        Assert.Contains("is not one of the site's keys now", WebUtility.HtmlDecode(html));
    }

    [Fact]
    public void ZA214_the_global_settings_rows_start_with_no_value()
    {
        var edits = new ApiKeyEdits(Keys);

        Assert.Equal(["Maps", "TinyMCE"], edits.Rows.Select(x => x.Name));
        Assert.All(edits.Rows, row => Assert.Null(row.NewValue));
    }

    [Fact]
    public void ZA214_an_empty_box_keeps_the_saved_value_and_a_typed_one_replaces_it()
    {
        var edits = new ApiKeyEdits(Keys);
        edits.Rows.Single(x => x.Name == "TinyMCE").NewValue = "new-tiny-key";
        // Renamed: the value it was saved with goes with it
        edits.Rows.Single(x => x.Name == "Maps").Name = "GoogleMaps";
        Assert.Null(edits.Add("Stripe", "pk-test", Text));

        var (keys, problem) = edits.ToSave(Text);

        Assert.Null(problem);
        Assert.Equal(Secret, keys!["GoogleMaps"]);
        Assert.Equal("new-tiny-key", keys["TinyMCE"]);
        Assert.Equal("pk-test", keys["Stripe"]);
        Assert.False(keys.ContainsKey("Maps"));

        edits.Load(keys);
        Assert.All(edits.Rows, row => Assert.Null(row.NewValue));
    }

    [Theory]
    [InlineData("", "a-value", "Every API key needs a name")]
    [InlineData("Extra", "", "The API key Extra has no value")]
    [InlineData("maps", "a-value", "Two API keys are named maps")]
    public void ZA214_a_key_without_a_name_or_value_or_with_a_taken_name_is_refused(string name, string value,
        string expected)
    {
        var edits = new ApiKeyEdits(Keys);
        edits.Rows.Add(new ApiKeyRow { Name = name, NewValue = value });

        var (keys, problem) = edits.ToSave(Text);

        Assert.Null(keys);
        Assert.Equal(expected, problem);
        Assert.Equal("There is already a key named maps", edits.Add("maps", "x", Text));
    }

    private static ServiceProvider CreateTextServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        AdminRenderer.AddImportedServices(services);
        return services.BuildServiceProvider();
    }

    private static Task<string> RenderPickerAsync(string? value, List<string> changes) =>
        AdminRenderer.RenderAsync<ApiKeysPickerProperty>(services =>
            services.AddSingleton(Fake<IApiKeyService>.Create((method, args) => method.Name switch
            {
                nameof(IApiKeyService.GetApiKeyNamesAsync) =>
                    Task.FromResult<IReadOnlyList<string>>(Keys.Keys.Order().ToList()),
                nameof(IApiKeyService.GetApiKeyAsync) =>
                    Task.FromResult(Keys.GetValueOrDefault((string)args![0]!)),
                _ => throw new InvalidOperationException($"{method.Name} was not expected")
            })), new Dictionary<string, object?>
            {
                [nameof(ApiKeysPickerProperty.Value)] = value,
                [nameof(ApiKeysPickerProperty.ValueChanged)] =
                    EventCallback.Factory.Create<string>(new object(), changes.Add)
            });
}
