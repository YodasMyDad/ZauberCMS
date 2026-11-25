using ZauberCMS.Components.Editors.ToolbarItems;

namespace ZauberCMS.Components.Tests.Editors;

/// <summary>
/// ZA-135: editing an existing content link on the Manual tab must keep its <c>data-contentid</c>. The editor only
/// reports the link's href, so the toolbar item finds the content id in the editor HTML.
/// </summary>
public class ContentLinkTests
{
    private static readonly Guid BlogId = Guid.Parse("6f9d4f1e-8a51-4c43-9b6f-0f9f0a8f2c11");

    [Fact]
    public void The_content_id_of_the_link_being_edited_is_found_by_its_href()
    {
        var html = $"<p>Read the <a href=\"/blog\" data-contentid=\"{BlogId}\">blog</a> and <a href=\"/about\">about</a>.</p>";

        Assert.Equal(BlogId, InsertContentLinkItem.FindContentId(html, "/blog"));
    }

    [Theory]
    [InlineData("/about")]
    [InlineData("/missing")]
    [InlineData("")]
    public void A_link_without_a_content_id_gives_none(string href)
    {
        var html = $"<p><a href=\"/blog\" data-contentid=\"{BlogId}\">blog</a> <a href=\"/about\">about</a></p>";

        Assert.Null(InsertContentLinkItem.FindContentId(html, href));
    }

    [Fact]
    public void An_encoded_href_still_matches()
    {
        var html = $"<a href=\"/search?a=1&amp;b=2\" data-contentid=\"{BlogId}\">search</a>";

        Assert.Equal(BlogId, InsertContentLinkItem.FindContentId(html, "/search?a=1&b=2"));
    }
}
