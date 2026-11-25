using Microsoft.EntityFrameworkCore;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Tests.Infrastructure;

namespace ZauberCMS.Core.Tests.Content;

public class ContentSaveTests : IAsyncLifetime
{
    private TestSite _site = null!;
    private ContentType _pageType = null!;

    public async ValueTask InitializeAsync()
    {
        _site = await TestSite.CreateAsync();
        _pageType = await _site.CreateContentTypeAsync("page");
    }

    public async ValueTask DisposeAsync() => await _site.DisposeAsync();

    [Fact]
    public async Task New_content_is_stored_with_a_slug_and_the_signed_in_user()
    {
        var page = await _site.CreateContentAsync(_pageType, "About Us");

        var stored = await _site.QueryDatabaseAsync(db => db.Contents.AsNoTracking().SingleAsync(x => x.Id == page.Id));
        Assert.Equal("about-us", stored.Url());
        Assert.Equal("page", stored.ContentTypeAlias);
        Assert.Equal(_site.CurrentUser.Id, stored.LastUpdatedById);
    }

    [Fact]
    public async Task Two_pages_with_the_same_name_get_different_urls()
    {
        var first = await _site.CreateContentAsync(_pageType, "News");
        var second = await _site.CreateContentAsync(_pageType, "News");

        var stored = await _site.QueryDatabaseAsync(db => db.Contents.AsNoTracking()
            .Where(x => x.Id == first.Id || x.Id == second.Id).ToListAsync());

        Assert.Equal(2, stored.Select(x => x.Url()).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }
}
