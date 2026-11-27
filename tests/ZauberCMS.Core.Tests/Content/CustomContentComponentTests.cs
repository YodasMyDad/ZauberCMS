using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging.Abstractions;
using ZauberCMS.Components.ContentComponents;
using ZauberCMS.Core.Content.Interfaces;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Tests.Infrastructure;
using ContentItem = ZauberCMS.Core.Content.Models.Content;

namespace ZauberCMS.Core.Tests.Content;

/// <summary>
/// ZA-126 (Roadmap D58): a custom component is given the id of its page, not the page, because an interactive
/// component's parameters are serialised into the HTML.
/// </summary>
public class CustomContentComponentTests : IAsyncLifetime
{
    private TestSite _site = null!;

    public async ValueTask InitializeAsync() => _site = await TestSite.CreateAsync();

    public async ValueTask DisposeAsync() => await _site.DisposeAsync();

    [Fact]
    public void ZA126_the_contract_has_no_member_that_carries_the_page()
    {
        Assert.DoesNotContain(typeof(ICustomContentComponent).GetProperties(),
            p => typeof(ContentItem).IsAssignableFrom(p.PropertyType));
        Assert.Equal(typeof(Guid), typeof(ICustomContentComponent).GetProperty(nameof(ICustomContentComponent.ContentId))!.PropertyType);
    }

    [Fact]
    public async Task ZA126_RenderCustomContent_passes_only_the_page_id()
    {
        var pageType = await _site.CreateContentTypeAsync("page");
        var page = await _site.CreateContentAsync(pageType, "Contact");

        using var scope = _site.CreateScope();
        await using var renderer = new HtmlRenderer(scope.ServiceProvider, NullLoggerFactory.Instance);
        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            // The probe declares ContentId only: a "Content" parameter would make the render throw
            var output = await renderer.RenderComponentAsync<RenderCustomContent>(ParameterView.FromDictionary(
                new Dictionary<string, object?>
                {
                    [nameof(RenderCustomContent.Content)] = page,
                    [nameof(RenderCustomContent.CustomComponents)] =
                        new List<string> { typeof(ProbeCustomComponent).FullName! }
                }));
            return output.ToHtmlString();
        });

        Assert.Equal($"<p>page {page.Id}</p>", html.Trim());
    }
}

/// <summary>A custom component that shows the id it was given (discovered for every test; only rendered above).</summary>
public class ProbeCustomComponent : ComponentBase, ICustomContentComponent
{
    public string Name => "Probe";

    [Parameter] public Guid ContentId { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder) =>
        builder.AddMarkupContent(0, $"<p>page {ContentId}</p>");
}
