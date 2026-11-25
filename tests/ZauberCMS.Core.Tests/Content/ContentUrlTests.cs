using ZauberCMS.Core.Content;

namespace ZauberCMS.Core.Tests.Content;

/// <summary>
/// The one set of URL rules the lookup (ZA-066) and the rich-text link check (ZA-135) share.
/// </summary>
public class ContentUrlTests
{
    [Theory]
    [InlineData(null, "")]
    [InlineData("/", "")]
    [InlineData(" /About-Us/ ", "about-us")]
    [InlineData("Blog/Post", "blog/post")]
    [InlineData("https://Example.com/A", "https://Example.com/A")]
    public void Normalize_gives_the_stored_form(string? input, string expected) =>
        Assert.Equal(expected, ContentUrl.Normalize(input));

    [Theory]
    [InlineData("blog", "/blog")]
    [InlineData("/blog/", "/blog")]
    [InlineData("", null)]
    [InlineData("https://example.com", "https://example.com")]
    public void SiteRelative_adds_one_leading_slash(string input, string? expected) =>
        Assert.Equal(expected, ContentUrl.SiteRelative(input));

    // A stored URL never becomes a script address or a link to another host
    [Theory]
    [InlineData("javascript://%0aalert(1)", "/javascript://%0aalert(1)")]
    [InlineData(@"\evil.example", "/evil.example")]
    [InlineData("//evil.example", "/evil.example")]
    public void SiteRelative_keeps_unsafe_values_on_this_site(string input, string expected) =>
        Assert.Equal(expected, ContentUrl.SiteRelative(input));

    // ZA-135: the dialog stores /blog, the content's URL is blog. Same place, so nothing to rewrite.
    [Theory]
    [InlineData("/blog")]
    [InlineData("blog")]
    [InlineData("/Blog/")]
    [InlineData("/blog#comments")]
    [InlineData("/blog?page=2")]
    public void A_link_that_already_points_at_the_content_is_left_alone(string href) =>
        Assert.Null(ContentUrl.UpdatedHref(href, "blog"));

    [Fact]
    public void A_link_to_content_whose_url_changed_gets_the_new_site_relative_url() =>
        Assert.Equal("/news", ContentUrl.UpdatedHref("/blog", "news"));

    [Fact]
    public void A_query_string_or_fragment_is_kept_when_the_url_changes() =>
        Assert.Equal("/news#comments", ContentUrl.UpdatedHref("/blog#comments", "news"));

    [Fact]
    public void Content_without_a_url_never_rewrites_the_link() =>
        Assert.Null(ContentUrl.UpdatedHref("/blog", null));
}
