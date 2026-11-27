using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Content.Interfaces;
using ZauberCMS.Core.Content.Mapping;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Content.Parameters;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Shared.Models;
using ZauberCMS.Core.Tags.Interfaces;
using ZauberCMS.Core.Tags.Models;
using ZauberCMS.Core.Tags.Parameters;
using ZauberCMS.Core.Tests.Infrastructure;
using ContentItem = ZauberCMS.Core.Content.Models.Content;

namespace ZauberCMS.Core.Tests.Content;

/// <summary>
/// Roadmap Phase 10b, content data: tag links written by the page save (ZA-099), a content type import that keeps
/// its property ids (ZA-077), and a restored page's draft that follows its new URL (ZA-144).
/// </summary>
public class ContentDataTests : IAsyncLifetime
{
    protected virtual string DatabaseProvider => TestSite.SqliteProvider;

    private TestSite _site = null!;

    public async ValueTask InitializeAsync() =>
        _site = await TestSite.CreateAsync(databaseProvider: DatabaseProvider);

    public async ValueTask DisposeAsync() => await _site.DisposeAsync();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<T> WithContentService<T>(Func<IContentService, Task<T>> call)
    {
        using var scope = _site.CreateScope();
        return await call(scope.ServiceProvider.GetRequiredService<IContentService>());
    }

    private Task<ContentItem> StoredAsync(Guid id) => _site.QueryDatabaseAsync(db => db.Contents.AsNoTracking()
        .Include(x => x.PropertyData).SingleAsync(x => x.Id == id, Ct));

    private Task<List<Guid>> TagLinksAsync(Guid itemId) => _site.QueryDatabaseAsync(db => db.TagItems.AsNoTracking()
        .Where(x => x.ItemId == itemId).Select(x => x.TagId).ToListAsync(Ct));

    private async Task<ContentType> CreateTypeAsync(string alias, params PropertyType[] properties)
    {
        var result = await WithContentService(s => s.SaveContentTypeAsync(new SaveContentTypeParameters
        {
            ContentType = new ContentType
            {
                Name = alias, Alias = alias, AllowAtRoot = true, ContentProperties = [.. properties]
            }
        }, Ct));
        Assert.True(result.Success, TestSite.Describe(result.Messages));
        return result.Entity!;
    }

    private async Task<Tag> CreateTagAsync(string name)
    {
        using var scope = _site.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<ITagService>()
            .SaveTagAsync(new SaveTagParameters { TagName = name }, Ct);
        Assert.True(result.Success, TestSite.Describe(result.Messages));
        return result.Entity!;
    }

    private static string TagValue(params Tag[] tags) =>
        System.Text.Json.JsonSerializer.Serialize(tags.Select(t => t.Id).ToList());

    private static PropertyType TagsProperty() => new()
    {
        Alias = "tags", Name = "Tags", ComponentAlias = "ZauberCMS.Tags",
        Component = "ZauberCMS.Components.Editors.TagsProperty"
    };

    private static PropertyType TextBox(string alias) => new()
    {
        Alias = alias, Name = alias, ComponentAlias = "ZauberCMS.Textbox",
        Component = "ZauberCMS.Components.Editors.TextBox"
    };

    private Task<HandlerResult<ContentItem>> SaveAsync(ContentItem content, bool draftOnly = false) =>
        WithContentService(s => s.SaveContentAsync(
            new SaveContentParameters { Content = content, SaveUnpublishedOnly = draftOnly }, Ct));

    [Fact]
    public async Task ZA099_The_page_save_writes_its_tag_links_from_the_published_value_and_a_draft_leaves_them()
    {
        var type = await CreateTypeAsync("post", TagsProperty());
        var news = await CreateTagAsync("News");
        var events = await CreateTagAsync("Events");
        var page = new ContentItem
        {
            Name = "Post", ContentTypeId = type.Id, ContentTypeAlias = type.Alias, IsRootContent = true,
            Published = true
        };
        page.PropertyData.Add(new ContentPropertyValue
        {
            ContentId = page.Id, Alias = "tags", ContentTypePropertyId = type.ContentProperties[0].Id,
            Value = TagValue(news)
        });

        var created = await SaveAsync(page);
        Assert.True(created.Success, TestSite.Describe(created.Messages));
        Assert.Equal([news.Id], await TagLinksAsync(page.Id));

        // "Save only": the live site keeps its tags, as in Umbraco
        var copy = await StoredAsync(page.Id);
        copy.PropertyData.Single().Value = TagValue(news, events);
        var drafted = await SaveAsync(copy, draftOnly: true);
        Assert.True(drafted.Success, TestSite.Describe(drafted.Messages));
        Assert.Equal([news.Id], await TagLinksAsync(page.Id));

        // Published with only the second tag
        copy = await StoredAsync(page.Id);
        copy.PropertyData.Single().Value = TagValue(events);
        var published = await SaveAsync(copy);
        Assert.True(published.Success, TestSite.Describe(published.Messages));
        Assert.Equal([events.Id], await TagLinksAsync(page.Id));
        var tagged = await WithContentService(s => s.QueryContentAsync(new QueryContentParameters
        {
            TagSlugs = [events.Slug], Cached = false
        }, Ct));
        Assert.Contains(tagged.Items, x => x.Id == page.Id);

        // A permanent delete takes the links with it
        var deleted = await WithContentService(s => s.DeleteContentAsync(
            new DeleteContentParameters { ContentId = page.Id, MoveToRecycleBin = false }, Ct));
        Assert.True(deleted.Success, TestSite.Describe(deleted.Messages));
        Assert.Empty(await TagLinksAsync(page.Id));
    }

    [Fact]
    public async Task ZA099_A_block_added_in_a_draft_gets_its_tag_links_when_the_page_is_published()
    {
        var blockType = await CreateTypeAsync("tagged", TagsProperty());
        var blocksProperty = new PropertyType
        {
            Alias = "blocks", Name = "blocks",
            ComponentAlias = ZauberCMS.Core.Content.Services.BlockListNestedContentReferenceProvider.BlockListEditorAlias,
            Component = "ZauberCMS.Components.Editors.BlockListEditorProperty"
        };
        var pageType = await CreateTypeAsync("landing", blocksProperty);
        var news = await CreateTagAsync("News");
        var page = new ContentItem
        {
            Name = "Landing", ContentTypeId = pageType.Id, ContentTypeAlias = pageType.Alias, IsRootContent = true,
            Published = true
        };
        page.PropertyData.Add(new ContentPropertyValue
        {
            ContentId = page.Id, Alias = "blocks", ContentTypePropertyId = pageType.ContentProperties[0].Id,
            Value = "[]"
        });
        Assert.True((await SaveAsync(page)).Success);

        // "Save only" with a new block: the live site has no such block, so no links
        var block = new ContentItem
        {
            Name = "Tagged block", ContentTypeId = blockType.Id, ContentTypeAlias = blockType.Alias,
            IsNestedContent = true, Published = true
        };
        block.PropertyData.Add(new ContentPropertyValue
        {
            ContentId = block.Id, Alias = "tags", ContentTypePropertyId = blockType.ContentProperties[0].Id,
            Value = TagValue(news)
        });
        var draft = await StoredAsync(page.Id);
        draft.PropertyData.Single().Value = $"[\"{block.Id}\"]";
        var drafted = await WithContentService(s => s.SaveContentAsync(new SaveContentParameters
        {
            Content = draft, SaveUnpublishedOnly = true, Blocks = [block]
        }, Ct));
        Assert.True(drafted.Success, TestSite.Describe(drafted.Messages));
        Assert.Empty(await TagLinksAsync(block.Id));

        // Published later without the block being sent again (the editor sends only what it changed)
        var publish = await StoredAsync(page.Id);
        publish.PropertyData.Single().Value = $"[\"{block.Id}\"]";
        var published = await SaveAsync(publish);

        Assert.True(published.Success, TestSite.Describe(published.Messages));
        Assert.Equal([news.Id], await TagLinksAsync(block.Id));
    }

    [Fact]
    public async Task ZA099_A_value_naming_a_deleted_tag_does_not_break_the_save()
    {
        var type = await CreateTypeAsync("post", TagsProperty());
        var page = new ContentItem
        {
            Name = "Post", ContentTypeId = type.Id, ContentTypeAlias = type.Alias, IsRootContent = true,
            Published = true
        };
        page.PropertyData.Add(new ContentPropertyValue
        {
            ContentId = page.Id, Alias = "tags", ContentTypePropertyId = type.ContentProperties[0].Id,
            Value = $"[\"{Guid.NewGuid()}\"]"
        });

        var saved = await SaveAsync(page);

        Assert.True(saved.Success, TestSite.Describe(saved.Messages));
        Assert.Empty(await TagLinksAsync(page.Id));
    }

    [Fact]
    public async Task ZA077_Importing_a_content_type_over_its_alias_keeps_its_property_and_tab_ids()
    {
        var type = await CreateTypeAsync("article", TextBox("title"));
        var page = new ContentItem
        {
            Name = "Article", ContentTypeId = type.Id, ContentTypeAlias = type.Alias, IsRootContent = true,
            Published = true
        };
        page.PropertyData.Add(new ContentPropertyValue
        {
            ContentId = page.Id, Alias = "title", ContentTypePropertyId = type.ContentProperties[0].Id,
            Value = "Hello"
        });
        Assert.True((await SaveAsync(page)).Success);
        var json = await WithContentService(s => s.ExportContentTypeAsync("article"));

        var imported = await WithContentService(s => s.ImportContentTypeAsync(json!));

        Assert.True(imported.Success, TestSite.Describe(imported.Messages));
        var stored = await _site.QueryDatabaseAsync(db =>
            db.ContentTypes.AsNoTracking().SingleAsync(x => x.Alias == "article", Ct));
        Assert.Equal(type.ContentProperties.Select(p => p.Id), stored.ContentProperties.Select(p => p.Id));
        Assert.Equal(type.Tabs.Select(t => t.Id).Order(), stored.Tabs.Select(t => t.Id).Order());

        // The value still belongs to a property of the type, so the next save keeps it
        var copy = await StoredAsync(page.Id);
        copy.Name = "Article (edited)";
        Assert.True((await SaveAsync(copy)).Success);
        Assert.Equal("Hello", (await StoredAsync(page.Id)).PropertyData.Single().Value);
    }

    [Fact]
    public async Task ZA144_A_restore_that_changes_the_url_gives_the_pending_draft_the_new_url_too()
    {
        var type = await _site.CreateContentTypeAsync("page");
        var offers = await _site.CreateContentAsync(type, "Offers");
        var copy = await StoredAsync(offers.Id);
        copy.Name = "Offers (draft)";
        Assert.True((await SaveAsync(copy, draftOnly: true)).Success);
        Assert.True((await WithContentService(s => s.DeleteContentAsync(
            new DeleteContentParameters { ContentId = offers.Id, MoveToRecycleBin = true }, Ct))).Success);
        // Meanwhile another page takes the URL
        await _site.CreateContentAsync(type, "Offers");

        var binned = await StoredAsync(offers.Id);
        binned.Deleted = false;
        var restored = await WithContentService(s => s.SaveContentAsync(
            new SaveContentParameters { Content = binned, ExcludePropertyData = true }, Ct));

        Assert.True(restored.Success, TestSite.Describe(restored.Messages));
        var stored = await StoredAsync(offers.Id);
        Assert.Equal("offers-1", stored.Url);
        var draft = await _site.QueryDatabaseAsync(db => db.UnpublishedContent.AsNoTracking()
            .SingleAsync(x => x.Id == stored.UnpublishedContentId, Ct));
        Assert.Equal("offers-1", draft.JsonContent.Url);
        // So the draft can be published
        var editorCopy = draft.JsonContent.MapDraftTo(stored);
        editorCopy.UnpublishedContentId = stored.UnpublishedContentId;
        var published = await SaveAsync(editorCopy);
        Assert.True(published.Success, TestSite.Describe(published.Messages));
    }
}

/// <summary>The same tests on a real PostgreSQL (Roadmap D15); skipped without Docker.</summary>
public class ContentDataTestsOnPostgreSql : ContentDataTests
{
    protected override string DatabaseProvider => TestSite.PostgreSqlProvider;
}

/// <summary>The same tests on SQL Server; skipped without <c>ZAUBER_TEST_SQLSERVER</c>.</summary>
public class ContentDataTestsOnSqlServer : ContentDataTests
{
    protected override string DatabaseProvider => TestSite.SqlServerProvider;
}

/// <summary>
/// ZA-146: versions of one page written at the same moment are numbered one after the other and leave one current
/// published version. Only on the server databases: in-memory SQLite refuses a second writer instead of waiting.
/// </summary>
public abstract class ContentVersionRaceTests : IAsyncLifetime
{
    protected abstract string DatabaseProvider { get; }

    private TestSite _site = null!;

    public async ValueTask InitializeAsync() =>
        _site = await TestSite.CreateAsync(databaseProvider: DatabaseProvider);

    public async ValueTask DisposeAsync() => await _site.DisposeAsync();

    [Fact]
    public async Task ZA146_Versions_written_at_the_same_moment_are_all_stored_with_their_own_numbers()
    {
        var ct = TestContext.Current.CancellationToken;
        var type = await _site.CreateContentTypeAsync("page");
        var page = await _site.CreateContentAsync(type, "Busy page");

        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Task.Run(async () =>
        {
            using var scope = _site.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<IContentVersioningService>().CreateVersionAsync(
                new CreateContentVersionParameters { Content = page, Status = ContentVersionStatus.Published }, ct);
        }, ct)));

        Assert.All(results, r => Assert.True(r.Success, TestSite.Describe(r.Messages)));
        var versions = await _site.QueryDatabaseAsync(db => db.ContentVersions.AsNoTracking()
            .Where(v => v.ContentId == page.Id).ToListAsync(ct));
        Assert.Equal(5, versions.Count);
        Assert.Equal(Enumerable.Range(1, 5), versions.Select(v => v.VersionNumber).Order());
        Assert.Single(versions, v => v.IsCurrentPublished);
    }
}

public class ContentVersionRaceTestsOnPostgreSql : ContentVersionRaceTests
{
    protected override string DatabaseProvider => TestSite.PostgreSqlProvider;
}

public class ContentVersionRaceTestsOnSqlServer : ContentVersionRaceTests
{
    protected override string DatabaseProvider => TestSite.SqlServerProvider;
}
