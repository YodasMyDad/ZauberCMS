using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Seo;
using ZauberCMS.Core.Shared.Services;
using ZauberCMS.Core.Tests.Infrastructure;
using ZauberCMS.RTE.Models;

namespace ZauberCMS.Core.Tests.Shared;

/// <summary>
/// The rich text editor stores a video as a placeholder, never as an iframe. Rendering turns the placeholder into
/// a player, and only for a provider the site allows (ZA-134).
/// </summary>
public class RichTextEmbedTests
{
    private const string YouTube =
        """<figure data-embed="youtube" data-embed-id="dQw4w9WgXcQ" data-embed-url="https://youtu.be/dQw4w9WgXcQ"></figure>""";

    private readonly DefaultRichTextEmbedService _embeds = new();

    [Fact]
    public void A_placeholder_becomes_the_providers_player()
    {
        var html = _embeds.Expand($"<p>Before</p>{YouTube}<p>After</p>");

        Assert.StartsWith("<p>Before</p><figure class=\"zauber-embed\" data-embed=\"youtube\"><iframe", html);
        Assert.Contains("src=\"https://www.youtube-nocookie.com/embed/dQw4w9WgXcQ\"", html);
        Assert.Contains("width=\"560\"", html);
        Assert.Contains("height=\"315\"", html);
        Assert.EndsWith("</iframe></figure><p>After</p>", html);
        Assert.DoesNotContain("data-embed-id", html);
    }

    [Fact]
    public void Vimeo_is_built_in()
    {
        var html = _embeds.Expand("""<figure data-embed="vimeo" data-embed-id="76979871"></figure>""");

        Assert.Contains("src=\"https://player.vimeo.com/video/76979871\"", html);
    }

    [Theory]
    [InlineData("""<figure data-embed="evil" data-embed-id="abc"></figure>""")]
    [InlineData("""<figure data-embed="youtube" data-embed-id="a&quot; onload=&quot;alert(1)"></figure>""")]
    [InlineData("""<figure data-embed="youtube" data-embed-id="../../watch?v=x"></figure>""")]
    [InlineData("""<figure data-embed="youtube"></figure>""")]
    public void A_placeholder_for_an_unknown_provider_or_with_a_bad_id_is_removed(string placeholder)
    {
        var html = _embeds.Expand($"<p>Text</p>{placeholder}");

        Assert.Equal("<p>Text</p>", html);
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("<p>No video here</p>", "<p>No video here</p>")]
    [InlineData("<figure><img src=\"/a.png\"><figcaption>A picture</figcaption></figure>", "<figure><img src=\"/a.png\"><figcaption>A picture</figcaption></figure>")]
    public void Text_without_a_placeholder_is_returned_as_it_is(string? input, string expected)
    {
        Assert.Equal(expected, _embeds.Expand(input));
    }

    [Fact]
    public void Expanding_twice_gives_the_same_result()
    {
        var once = _embeds.Expand(YouTube);

        Assert.Equal(once, _embeds.Expand(once));
    }

    [Fact]
    public void The_sanitiser_keeps_the_placeholder_and_returns_it_as_a_player()
    {
        var html = new DefaultHtmlSanitizerService().Sanitize($"<p>Watch</p>{YouTube}<script>alert(1)</script>");

        Assert.Contains("<iframe", html);
        Assert.Contains("https://www.youtube-nocookie.com/embed/dQw4w9WgXcQ", html);
        Assert.DoesNotContain("<script", html);
    }

    [Fact]
    public void The_sanitiser_still_removes_an_iframe_from_another_site()
    {
        var html = new DefaultHtmlSanitizerService().Sanitize(
            $"""{YouTube}<iframe src="https://evil.example/x"></iframe>""");

        Assert.Contains("youtube-nocookie.com", html);
        Assert.DoesNotContain("evil.example", html);
    }

    [Fact]
    public void A_site_can_add_a_provider_by_replacing_the_service()
    {
        var html = new DefaultHtmlSanitizerService(null, new WithLoom()).Sanitize(
            """<figure data-embed="loom" data-embed-id="0123456789abcdef0123456789abcdef"></figure>""");

        Assert.Contains("src=\"https://www.loom.com/embed/0123456789abcdef0123456789abcdef\"", html);
    }

    [Fact]
    public async Task The_site_registers_the_embed_service_and_the_sanitiser_uses_it()
    {
        await using var site = await TestSite.CreateAsync();
        using var scope = site.CreateScope();

        var embeds = scope.ServiceProvider.GetRequiredService<IRichTextEmbedService>();
        var sanitiser = scope.ServiceProvider.GetRequiredService<IHtmlSanitizerService>();

        Assert.Contains(embeds.Providers, p => p.Name == "youtube");
        Assert.Contains(embeds.Providers, p => p.Name == "vimeo");
        Assert.Contains("<iframe", sanitiser.Sanitize(YouTube));
    }

    [Theory]
    [InlineData("new-page", "/new-page")]
    [InlineData(" /new-page ", "/new-page")]
    [InlineData("https://partner.example/landing", "https://partner.example/landing")]
    [InlineData("HTTP://partner.example", "HTTP://partner.example")]
    [InlineData("javascript:alert(1)", "/javascript:alert(1)")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void A_typed_redirect_target_keeps_a_full_address_and_makes_anything_else_a_local_path(string? typed, string expected)
    {
        Assert.Equal(expected, RedirectTarget.Normalize(typed));
    }

    private sealed class WithLoom : DefaultRichTextEmbedService
    {
        public override IReadOnlyList<EmbedProvider> Providers { get; } =
        [
            .. EmbedProvider.Defaults,
            new EmbedProvider
            {
                Name = "loom",
                DisplayName = "Loom",
                UrlPatterns = [@"^https://(?:www\.)?loom\.com/(?:share|embed)/(?<id>[a-f0-9]{32})(?=[/?#]|$)"],
                EmbedUrlTemplate = "https://www.loom.com/embed/{id}"
            }
        ];
    }
}
