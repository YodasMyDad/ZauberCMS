using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ZauberCMS.Components.Seo;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Seo.Models;
using ZauberCMS.Core.Tests.Infrastructure;
using ContentItem = ZauberCMS.Core.Content.Models.Content;

namespace ZauberCMS.Core.Tests.Seo;

/// <summary>
/// ZA-171: <c>RenderSeoTags</c> gives a page served through a content finder the address asked for as its canonical
/// URL and og:url (without the query string); a page found by its URL keeps its own.
/// </summary>
public class SeoTagsTests : IAsyncLifetime
{
    private TestSite _site = null!;

    public async ValueTask InitializeAsync() => _site = await TestSite.CreateAsync();

    public async ValueTask DisposeAsync() => await _site.DisposeAsync();

    [Fact]
    public async Task ZA171_a_page_served_through_a_finder_is_canonical_at_the_address_asked_for()
    {
        var html = await RenderAsync("http://localhost/products/abc-123?ref=mail",
            new EntryModel { ContentFinder = "MySite.ProductContentFinder" });

        Assert.Contains("<link rel=\"canonical\" href=\"http://localhost/products/abc-123\">", html);
        Assert.Contains("<meta property=\"og:url\" content=\"http://localhost/products/abc-123\">", html);
        Assert.DoesNotContain("product-page", html);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ZA171_a_page_found_by_its_url_keeps_its_own_canonical(bool cascadeEntry)
    {
        var html = await RenderAsync("http://localhost/product-page?ref=mail", cascadeEntry ? new EntryModel() : null);

        Assert.Contains("<link rel=\"canonical\" href=\"http://localhost/product-page\">", html);
        Assert.Contains("<meta property=\"og:url\" content=\"http://localhost/product-page\">", html);
    }

    private async Task<string> RenderAsync(string address, EntryModel? entry)
    {
        var page = new ContentItem
        {
            Name = "Product page",
            Url = "product-page",
            PropertyData =
            [
                new ContentPropertyValue
                {
                    Alias = "seo", Value = JsonSerializer.Serialize(new Meta { PageTitle = "Products" })
                }
            ]
        };

        using var scope = _site.CreateScope();
        var navigation = scope.ServiceProvider.GetRequiredService<NavigationManager>();
        ((IHostEnvironmentNavigationManager)navigation).Initialize("http://localhost/", address);

        await using var renderer = new HtmlRenderer(scope.ServiceProvider, NullLoggerFactory.Instance);
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var tags = new Dictionary<string, object?>
            {
                [nameof(RenderSeoTags.SeoAlias)] = "seo",
                [nameof(RenderSeoTags.Content)] = page
            };
            var output = entry == null
                ? await renderer.RenderComponentAsync<RenderSeoTags>(ParameterView.FromDictionary(tags))
                : await renderer.RenderComponentAsync<CascadingValue<EntryModel>>(ParameterView.FromDictionary(
                    new Dictionary<string, object?>
                    {
                        [nameof(CascadingValue<EntryModel>.Value)] = entry,
                        [nameof(CascadingValue<EntryModel>.ChildContent)] = (RenderFragment)(builder =>
                        {
                            builder.OpenComponent<RenderSeoTags>(0);
                            builder.AddComponentParameter(1, nameof(RenderSeoTags.SeoAlias), "seo");
                            builder.AddComponentParameter(2, nameof(RenderSeoTags.Content), page);
                            builder.CloseComponent();
                        })
                    }));
            return output.ToHtmlString();
        });
    }
}
