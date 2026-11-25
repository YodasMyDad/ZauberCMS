using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Content.Interfaces;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Content.Parameters;
using ZauberCMS.Core.Shared.Models;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Tests.Infrastructure;

using ContentItem = ZauberCMS.Core.Content.Models.Content;

namespace ZauberCMS.Core.Tests.Content;

public class ContentSaveTests : IAsyncLifetime
{
    /// <summary>Overridden by the PostgreSQL copy of this class at the end of the file.</summary>
    protected virtual string DatabaseProvider => TestSite.SqliteProvider;

    private TestSite _site = null!;
    private ContentType _pageType = null!;

    public async ValueTask InitializeAsync()
    {
        _site = await TestSite.CreateAsync(databaseProvider: DatabaseProvider);
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

    // A loaded copy may be saved once. This is why a dialog and the menu that opened it must not both save the
    // same object (ZA-076): the second save is, correctly, refused as an edit of an outdated copy.
    [Fact]
    public async Task Saving_an_outdated_copy_is_refused_and_does_not_overwrite_the_newer_save()
    {
        var page = await _site.CreateContentAsync(_pageType, "Home");
        var copy = await LoadAsync(page.Id);

        copy.Name = "First save";
        var first = await SaveAsync(copy);
        Assert.True(first.Success, TestSite.Describe(first.Messages));

        copy.Name = "Second save of the same copy";
        var second = await SaveAsync(copy);

        Assert.False(second.Success);
        Assert.Equal("First save", await _site.QueryDatabaseAsync(db =>
            db.Contents.AsNoTracking().Where(x => x.Id == page.Id).Select(x => x.Name).SingleAsync()));
    }

    [Fact]
    public async Task Saving_without_property_data_changes_the_content_and_keeps_its_language()
    {
        var page = await _site.CreateContentAsync(_pageType, "Home");
        var languageId = await _site.QueryDatabaseAsync(async db =>
        {
            var language = new ZauberCMS.Core.Languages.Models.Language { LanguageIsoCode = "de-DE", LanguageCultureName = "German (Germany)" };
            db.Languages.Add(language);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            return language.Id;
        });
        var copy = await LoadAsync(page.Id);

        copy.LanguageId = languageId;
        var saved = await SaveAsync(copy, excludePropertyData: true);

        Assert.True(saved.Success, TestSite.Describe(saved.Messages));
        Assert.Equal(languageId, await _site.QueryDatabaseAsync(db =>
            db.Contents.AsNoTracking().Where(x => x.Id == page.Id).Select(x => x.LanguageId).SingleAsync()));
    }

    private async Task<ContentItem> LoadAsync(Guid id)
    {
        using var scope = _site.CreateScope();
        var content = await scope.ServiceProvider.GetRequiredService<IContentService>()
            .GetContentAsync(new GetContentParameters { Id = id, IncludeUnpublished = true, Cached = false },
                TestContext.Current.CancellationToken);
        return content ?? throw new InvalidOperationException("Content not found.");
    }

    private async Task<HandlerResult<ContentItem>> SaveAsync(ContentItem content, bool excludePropertyData = false)
    {
        using var scope = _site.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IContentService>()
            .SaveContentAsync(new SaveContentParameters { Content = content, ExcludePropertyData = excludePropertyData },
                TestContext.Current.CancellationToken);
    }
}

/// <summary>The same tests on a real PostgreSQL (Roadmap D15); skipped without Docker.</summary>
public class ContentSaveTestsOnPostgreSql : ContentSaveTests
{
    protected override string DatabaseProvider => TestSite.PostgreSqlProvider;
}
