using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Content.Interfaces;
using ZauberCMS.Core.Content.Mapping;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Content.Parameters;
using ZauberCMS.Core.Content.Services;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Shared.Models;
using ZauberCMS.Core.Tests.Infrastructure;
using ContentItem = ZauberCMS.Core.Content.Models.Content;

namespace ZauberCMS.Core.Tests.Content;

/// <summary>
/// A page saved together with its blocks (<see cref="IContentService.SaveContentTreeAsync"/>), as the admin editor
/// saves it. The failure modes are in Docs/BlockListEditor.md §5.
/// </summary>
public class ContentTreeSaveTests : IAsyncLifetime
{
    /// <summary>Overridden by the PostgreSQL and SQL Server copies of this class at the end of the file.</summary>
    protected virtual string DatabaseProvider => TestSite.SqliteProvider;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private TestSite _site = null!;
    private ContentType _pageType = null!;
    private ContentType _blockType = null!;

    public async ValueTask InitializeAsync()
    {
        _site = await TestSite.CreateAsync(databaseProvider: DatabaseProvider);
        _pageType = await CreateTypeAsync("treePage", isElementType: false, BlockList("blocks"), BlockList("moreBlocks"));
        _blockType = await CreateTypeAsync("treeBlock", isElementType: true, TextBox("text"), BlockList("children"));
    }

    public async ValueTask DisposeAsync() => await _site.DisposeAsync();

    [Fact]
    public async Task A_new_page_is_saved_with_its_nested_blocks_in_one_audit_entry_and_one_version()
    {
        var child = NewBlock("child");
        var block = NewBlock("block", child);
        var page = NewPage("Home", blocks: [block]);
        var auditsBefore = await AuditCountAsync();

        var result = await SaveTreeAsync(page, [child, block]);

        Assert.True(result.Success, TestSite.Describe(result.Messages));
        var rows = await RowsAsync(block.Id, child.Id);
        Assert.All(rows.Values, r => Assert.True(r.IsNestedContent));
        Assert.Equal(page.Id, rows[block.Id].RelatedContentId);
        Assert.Equal(block.Id, rows[child.Id].RelatedContentId);
        Assert.Equal("child", await StoredValueAsync(child.Id, "text"));
        Assert.Equal(auditsBefore + 1, await AuditCountAsync());

        var version = Assert.Single(await VersionsAsync(page.Id));
        Assert.Equal(new[] { block.Id, child.Id }.Order(), version.BlockListSnapshots.Select(s => s.ContentId).Order());
        Assert.Empty(await VersionsAsync(block.Id));
    }

    // ZA-008: the version a save makes holds the blocks as that save wrote them
    [Fact]
    public async Task The_version_of_a_save_holds_the_block_values_of_that_save()
    {
        var (page, block) = await CreatePageWithBlockAsync("v1");

        var edited = await LoadAsync(block.Id);
        SetValue(edited, "text", "v2");
        var result = await SaveTreeAsync(await LoadAsync(page.Id), [edited]);

        Assert.True(result.Success, TestSite.Describe(result.Messages));
        var latest = (await VersionsAsync(page.Id)).MaxBy(v => v.VersionNumber)!;
        var snapshot = Assert.Single(latest.BlockListSnapshots);
        Assert.Equal("v2", snapshot.PropertySnapshots.Single(p => p.Alias == "text").Value);
    }

    // F4 / invariant 5: removing a block removes everything below it
    [Fact]
    public async Task Removing_a_block_deletes_it_and_the_blocks_below_it()
    {
        var child = NewBlock("child");
        var block = NewBlock("block", child);
        var keep = NewBlock("keep");
        var page = NewPage("Home", blocks: [block, keep]);
        Assert.True((await SaveTreeAsync(page, [child, block, keep])).Success);

        var reloaded = await LoadAsync(page.Id);
        SetValue(reloaded, "blocks", Ids(keep));
        var result = await SaveTreeAsync(reloaded, []);

        Assert.True(result.Success, TestSite.Describe(result.Messages));
        Assert.Equal([keep.Id], (await RowsAsync(block.Id, child.Id, keep.Id)).Keys.ToList());
        Assert.Equal(0, await _site.QueryDatabaseAsync(db => db.ContentPropertyValues
            .CountAsync(x => x.ContentId == block.Id || x.ContentId == child.Id, Ct)));
    }

    // F2: every block list of the page is saved, not only the last one touched
    [Fact]
    public async Task New_blocks_in_two_block_lists_are_both_saved()
    {
        var first = NewBlock("first");
        var second = NewBlock("second");
        var page = NewPage("Home", blocks: [first], moreBlocks: [second]);

        var result = await SaveTreeAsync(page, [first, second]);

        Assert.True(result.Success, TestSite.Describe(result.Messages));
        Assert.Equal(2, (await RowsAsync(first.Id, second.Id)).Count);
    }

    // F1: a block whose edit was cancelled is not in the values, so it is not saved
    [Fact]
    public async Task A_block_the_values_do_not_reach_is_not_saved()
    {
        var kept = NewBlock("kept");
        var cancelled = NewBlock("cancelled");
        var page = NewPage("Home", blocks: [kept]);

        var result = await SaveTreeAsync(page, [kept, cancelled]);

        Assert.True(result.Success, TestSite.Describe(result.Messages));
        Assert.Equal([kept.Id], (await RowsAsync(kept.Id, cancelled.Id)).Keys.ToList());
    }

    // F3: the newest values of a block are what is saved
    [Fact]
    public async Task Editing_a_new_block_again_before_saving_saves_the_last_values()
    {
        var child = NewBlock("first values");
        var block = NewBlock("block", child);
        var page = NewPage("Home", blocks: [block]);
        var childEditedAgain = NewBlock("second values");
        childEditedAgain.Id = child.Id;

        var result = await SaveTreeAsync(page, [block, child, childEditedAgain]);

        Assert.True(result.Success, TestSite.Describe(result.Messages));
        Assert.Equal("second values", await StoredValueAsync(child.Id, "text"));
    }

    // F5: the page and its blocks are saved together or not at all
    [Fact]
    public async Task A_save_that_fails_on_one_block_writes_nothing()
    {
        var (page, block) = await CreatePageWithBlockAsync("v1");
        var staleCopy = await LoadAsync(block.Id);
        var newerCopy = await LoadAsync(block.Id);
        SetValue(newerCopy, "text", "saved by someone else");
        Assert.True((await SaveTreeAsync(await LoadAsync(page.Id), [newerCopy])).Success);

        var added = NewBlock("added");
        var reloaded = await LoadAsync(page.Id);
        SetValue(reloaded, "blocks", Ids(block, added));
        SetValue(staleCopy, "text", "stale edit");
        var result = await SaveTreeAsync(reloaded, [staleCopy, added]);

        Assert.False(result.Success);
        Assert.Empty(await RowsAsync(added.Id));
        Assert.Equal("saved by someone else", await StoredValueAsync(block.Id, "text"));
        Assert.Equal(Ids(block), await StoredValueAsync(page.Id, "blocks"));
    }

    // F6: "Save only" of a published page changes nothing on the live site, blocks included; publishing does
    [Fact]
    public async Task A_draft_leaves_the_live_blocks_alone_until_it_is_published()
    {
        var (page, block) = await CreatePageWithBlockAsync("live");
        var removed = NewBlock("removed");
        var withRemoved = await LoadAsync(page.Id);
        SetValue(withRemoved, "blocks", Ids(block, removed));
        Assert.True((await SaveTreeAsync(withRemoved, [removed])).Success);

        var added = NewBlock("added in draft");
        var draftPage = await LoadAsync(page.Id);
        SetValue(draftPage, "blocks", Ids(block, added));
        var draftBlock = await LoadAsync(block.Id);
        SetValue(draftBlock, "text", "draft");
        var draft = await SaveTreeAsync(draftPage, [draftBlock, added], draftOnly: true);

        Assert.True(draft.Success, TestSite.Describe(draft.Messages));
        Assert.Equal(["live", "removed"], await LiveBlockTextsAsync(page.Id));
        Assert.Equal(Ids(block, removed), await StoredValueAsync(page.Id, "blocks"));

        // Publishing what the editor shows: the page's draft, with its blocks' drafts
        var publish = await SaveTreeAsync(await LoadAsDraftAsync(page.Id), []);

        Assert.True(publish.Success, TestSite.Describe(publish.Messages));
        Assert.Equal(["draft", "added in draft"], await LiveBlockTextsAsync(page.Id));
        Assert.Empty(await RowsAsync(removed.Id));
        Assert.Null((await RowsAsync(block.Id))[block.Id].UnpublishedContentId);
    }

    [Fact]
    public async Task Clearing_a_draft_drops_its_block_changes()
    {
        var (page, block) = await CreatePageWithBlockAsync("live");
        var added = NewBlock("added in draft");
        var draftPage = await LoadAsync(page.Id);
        SetValue(draftPage, "blocks", Ids(block, added));
        var draftBlock = await LoadAsync(block.Id);
        SetValue(draftBlock, "text", "draft");
        Assert.True((await SaveTreeAsync(draftPage, [draftBlock, added], draftOnly: true)).Success);

        var cleared = await WithContentService(s => s.ClearUnpublishedContentAsync(
            new ClearUnpublishedContentParameters { ContentId = page.Id }, Ct));

        Assert.True(cleared.Success, TestSite.Describe(cleared.Messages));
        Assert.Empty(await RowsAsync(added.Id));
        var storedBlock = (await RowsAsync(block.Id))[block.Id];
        Assert.Null(storedBlock.UnpublishedContentId);
        Assert.Equal(0, await _site.QueryDatabaseAsync(db => db.UnpublishedContent.CountAsync(Ct)));
        Assert.Equal(["live"], await LiveBlockTextsAsync(page.Id));
    }

    [Fact]
    public async Task A_block_of_other_content_is_refused()
    {
        var (_, othersBlock) = await CreatePageWithBlockAsync("someone else's");
        var page = NewPage("Mine");
        Assert.True((await SaveTreeAsync(page, [])).Success);

        var reloaded = await LoadAsync(page.Id);
        SetValue(reloaded, "blocks", Ids(othersBlock));
        var result = await SaveTreeAsync(reloaded, []);

        Assert.False(result.Success);
        Assert.Equal("[]", await StoredValueAsync(page.Id, "blocks"));
    }

    [Fact]
    public async Task Restoring_a_version_restores_its_blocks_and_drops_the_newer_ones()
    {
        var (page, block) = await CreatePageWithBlockAsync("v1");
        var firstVersion = Assert.Single(await VersionsAsync(page.Id));

        var added = NewBlock("added later");
        var reloaded = await LoadAsync(page.Id);
        SetValue(reloaded, "blocks", Ids(block, added));
        var edited = await LoadAsync(block.Id);
        SetValue(edited, "text", "v2");
        Assert.True((await SaveTreeAsync(reloaded, [edited, added])).Success);

        var restore = await WithVersioningService(s => s.PublishVersionAsync(
            new PublishContentVersionParameters { VersionId = firstVersion.Id }, Ct));

        Assert.True(restore.Success, TestSite.Describe(restore.Messages));
        Assert.Equal(["v1"], await LiveBlockTextsAsync(page.Id));
        Assert.Empty(await RowsAsync(added.Id));
    }

    [Fact]
    public async Task Deleting_a_page_deletes_the_blocks_its_draft_added()
    {
        var (page, block) = await CreatePageWithBlockAsync("live");
        var added = NewBlock("added in draft");
        var draftPage = await LoadAsync(page.Id);
        SetValue(draftPage, "blocks", Ids(block, added));
        Assert.True((await SaveTreeAsync(draftPage, [added], draftOnly: true)).Success);

        var deleted = await WithContentService(s => s.DeleteContentAsync(
            new DeleteContentParameters { ContentId = page.Id, MoveToRecycleBin = false }, Ct));

        Assert.True(deleted.Success, TestSite.Describe(deleted.Messages));
        Assert.Empty(await RowsAsync(page.Id, block.Id, added.Id));
    }

    [Fact]
    public async Task A_copy_of_a_page_gets_its_own_blocks()
    {
        var (page, block) = await CreatePageWithBlockAsync("original");

        var copy = await WithContentService(s => s.CopyContentAsync(
            new CopyContentParameters { ContentToCopy = page.Id }, Ct));

        Assert.True(copy.Success, TestSite.Describe(copy.Messages));
        var copiedIds = JsonSerializer.Deserialize<List<Guid>>((await StoredValueAsync(copy.Entity!.Id, "blocks"))!)!;
        var copiedBlock = Assert.Single(copiedIds);
        Assert.NotEqual(block.Id, copiedBlock);
        Assert.Equal(copy.Entity.Id, (await RowsAsync(copiedBlock))[copiedBlock].RelatedContentId);
        Assert.Equal("original", await StoredValueAsync(copiedBlock, "text"));
    }

    [Fact]
    public async Task Publishing_an_edited_block_that_has_a_draft_publishes_the_edit_and_drops_the_draft()
    {
        var (page, block) = await CreatePageWithBlockAsync("live");
        var draftBlock = await LoadAsync(block.Id);
        SetValue(draftBlock, "text", "draft");
        Assert.True((await SaveTreeAsync(await LoadAsync(page.Id), [draftBlock], draftOnly: true)).Success);

        var final = await LoadAsDraftAsync(block.Id);
        Assert.Equal("draft", final.GetValue<string>("text"));
        SetValue(final, "text", "final");
        var result = await SaveTreeAsync(await LoadAsDraftAsync(page.Id), [final]);

        Assert.True(result.Success, TestSite.Describe(result.Messages));
        Assert.Equal(["final"], await LiveBlockTextsAsync(page.Id));
        Assert.Equal(0, await _site.QueryDatabaseAsync(db => db.UnpublishedContent.CountAsync(Ct)));
    }

    [Fact]
    public async Task A_second_save_only_updates_the_drafts_it_made()
    {
        var (page, block) = await CreatePageWithBlockAsync("live");
        var first = await LoadAsync(block.Id);
        SetValue(first, "text", "first draft");
        Assert.True((await SaveTreeAsync(await LoadAsync(page.Id), [first], draftOnly: true)).Success);

        var second = await LoadAsDraftAsync(block.Id);
        SetValue(second, "text", "second draft");
        var result = await SaveTreeAsync(await LoadAsDraftAsync(page.Id), [second], draftOnly: true);

        Assert.True(result.Success, TestSite.Describe(result.Messages));
        Assert.Equal(2, await _site.QueryDatabaseAsync(db => db.UnpublishedContent.CountAsync(Ct)));
        Assert.Equal("second draft", (await LoadAsDraftAsync(block.Id)).GetValue<string>("text"));
        Assert.Equal(["live"], await LiveBlockTextsAsync(page.Id));
    }

    // A block only a draft reaches, below a block that is live: publish keeps it, clearing the draft deletes it
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_block_a_draft_adds_inside_a_live_block_follows_the_draft(bool publish)
    {
        var (page, block) = await CreatePageWithBlockAsync("parent");
        var child = NewBlock("added in draft");
        var draftParent = await LoadAsync(block.Id);
        SetValue(draftParent, "children", Ids(child));
        Assert.True((await SaveTreeAsync(await LoadAsync(page.Id), [draftParent, child], draftOnly: true)).Success);
        Assert.Equal("[]", await StoredValueAsync(block.Id, "children"));

        if (publish)
        {
            var result = await SaveTreeAsync(await LoadAsDraftAsync(page.Id), []);
            Assert.True(result.Success, TestSite.Describe(result.Messages));
            Assert.Equal(Ids(child), await StoredValueAsync(block.Id, "children"));
            Assert.Single(await RowsAsync(child.Id));
        }
        else
        {
            var cleared = await WithContentService(s => s.ClearUnpublishedContentAsync(
                new ClearUnpublishedContentParameters { ContentId = page.Id }, Ct));
            Assert.True(cleared.Success, TestSite.Describe(cleared.Messages));
            Assert.Empty(await RowsAsync(child.Id));
        }

        Assert.Equal(0, await _site.QueryDatabaseAsync(db => db.UnpublishedContent.CountAsync(Ct)));
    }

    // Versions made before blocks were saved with their page have no snapshot of the blocks that save added
    [Fact]
    public async Task Restoring_a_version_without_a_snapshot_of_a_block_keeps_the_block()
    {
        var (page, block) = await CreatePageWithBlockAsync("v1");
        var oldVersion = Assert.Single(await VersionsAsync(page.Id));
        await _site.QueryDatabaseAsync(async db =>
        {
            var version = await db.ContentVersions.SingleAsync(v => v.Id == oldVersion.Id, Ct);
            version.BlockListSnapshots = [];
            return await db.SaveChangesAsync(Ct);
        });

        var added = NewBlock("added later");
        var reloaded = await LoadAsync(page.Id);
        SetValue(reloaded, "blocks", Ids(block, added));
        Assert.True((await SaveTreeAsync(reloaded, [added])).Success);

        var restore = await WithVersioningService(s => s.PublishVersionAsync(
            new PublishContentVersionParameters { VersionId = oldVersion.Id }, Ct));

        Assert.True(restore.Success, TestSite.Describe(restore.Messages));
        Assert.Equal(["v1"], await LiveBlockTextsAsync(page.Id));
        Assert.Empty(await RowsAsync(added.Id));
    }

    // SaveContentAsync publishes the page's draft but leaves its blocks' drafts: the editor must still save it
    [Fact]
    public async Task A_page_published_with_the_single_item_save_can_still_be_saved_with_its_blocks()
    {
        var (page, block) = await CreatePageWithBlockAsync("parent");
        var child = NewBlock("added in draft");
        var draftParent = await LoadAsync(block.Id);
        SetValue(draftParent, "children", Ids(child));
        Assert.True((await SaveTreeAsync(await LoadAsync(page.Id), [draftParent, child], draftOnly: true)).Success);
        var single = await WithContentService(async s => await s.SaveContentAsync(
            new SaveContentParameters { Content = await LoadAsDraftAsync(page.Id) }, Ct));
        Assert.True(single.Success, TestSite.Describe(single.Messages));

        var result = await SaveTreeAsync(await LoadAsync(page.Id), []);

        Assert.True(result.Success, TestSite.Describe(result.Messages));
        Assert.Equal(Ids(child), await StoredValueAsync(block.Id, "children"));
    }

    // Two editors with the same page open: the one that saves a draft later must not overwrite the other's draft
    [Fact]
    public async Task A_save_from_a_copy_opened_before_someone_else_saved_a_draft_is_refused()
    {
        var (page, block) = await CreatePageWithBlockAsync("live");
        var staleCopy = await LoadAsync(page.Id);

        var added = NewBlock("other editor's draft");
        var otherCopy = await LoadAsync(page.Id);
        SetValue(otherCopy, "blocks", Ids(block, added));
        Assert.True((await SaveTreeAsync(otherCopy, [added], draftOnly: true)).Success);

        var result = await SaveTreeAsync(staleCopy, [], draftOnly: true);

        Assert.False(result.Success);
        Assert.Single(await RowsAsync(added.Id));
    }

    [Theory]
    [InlineData(NestedContentTreeLimit, true)]
    [InlineData(NestedContentTreeLimit + 1, false)]
    public async Task Blocks_nest_up_to_the_limit(int depth, bool saved)
    {
        var chain = new List<ContentItem>();
        ContentItem? below = null;
        for (var level = depth; level >= 1; level--)
        {
            below = below == null ? NewBlock($"level {level}") : NewBlock($"level {level}", below);
            chain.Add(below);
        }

        var result = await SaveTreeAsync(NewPage("Deep", blocks: [below!]), chain);

        Assert.Equal(saved, result.Success);
    }

    private const int NestedContentTreeLimit = 20;

    // ---- helpers ----

    private async Task<(ContentItem Page, ContentItem Block)> CreatePageWithBlockAsync(string text)
    {
        var block = NewBlock(text);
        var page = NewPage("Page " + Guid.NewGuid().ToString("N")[..6], blocks: [block]);
        var result = await SaveTreeAsync(page, [block]);
        Assert.True(result.Success, TestSite.Describe(result.Messages));
        return (page, block);
    }

    private static PropertyType BlockList(string alias) => new()
    {
        Alias = alias, Name = alias,
        ComponentAlias = BlockListNestedContentReferenceProvider.BlockListEditorAlias,
        Component = "ZauberCMS.Components.Editors.BlockListEditorProperty"
    };

    private static PropertyType TextBox(string alias) => new()
    {
        Alias = alias, Name = alias, ComponentAlias = "ZauberCMS.Textbox",
        Component = "ZauberCMS.Components.Editors.TextBox"
    };

    private async Task<ContentType> CreateTypeAsync(string alias, bool isElementType, params PropertyType[] properties)
    {
        var result = await WithContentService(s => s.SaveContentTypeAsync(new SaveContentTypeParameters
        {
            ContentType = new ContentType
            {
                Name = alias, Alias = alias, AllowAtRoot = !isElementType, IsElementType = isElementType,
                ContentProperties = [.. properties]
            }
        }));
        Assert.True(result.Success, TestSite.Describe(result.Messages));
        return result.Entity!;
    }

    private ContentItem NewPage(string name, ContentItem[]? blocks = null, ContentItem[]? moreBlocks = null)
    {
        var page = new ContentItem
        {
            Name = name, ContentTypeId = _pageType.Id, ContentTypeAlias = _pageType.Alias, IsRootContent = true,
            Published = true
        };
        AddValues(page, _pageType, ("blocks", Ids(blocks ?? [])), ("moreBlocks", Ids(moreBlocks ?? [])));
        return page;
    }

    private ContentItem NewBlock(string text, params ContentItem[] children)
    {
        var block = new ContentItem
        {
            Name = text, ContentTypeId = _blockType.Id, ContentTypeAlias = _blockType.Alias, IsNestedContent = true,
            Published = true
        };
        AddValues(block, _blockType, ("text", text), ("children", Ids(children)));
        return block;
    }

    private static void AddValues(ContentItem content, ContentType type, params (string Alias, string Value)[] values)
    {
        foreach (var (alias, value) in values)
        {
            content.PropertyData.Add(new ContentPropertyValue
            {
                ContentId = content.Id, Alias = alias, Value = value,
                ContentTypePropertyId = type.ContentProperties.Single(p => p.Alias == alias).Id
            });
        }
    }

    private static void SetValue(ContentItem content, string alias, string value)
    {
        content.PropertyData.Single(p => p.Alias == alias).Value = value;
        content.InvalidateContentValuesCache();
    }

    private static string Ids(params ContentItem[] items) => JsonSerializer.Serialize(items.Select(x => x.Id).ToList());

    private Task<HandlerResult<ContentItem>> SaveTreeAsync(ContentItem root, List<ContentItem> blocks,
        bool draftOnly = false) =>
        WithContentService(s => s.SaveContentTreeAsync(new SaveContentTreeParameters
        {
            Content = root, Blocks = blocks, SaveUnpublishedOnly = draftOnly
        }, Ct));

    private async Task<ContentItem> LoadAsync(Guid id) =>
        await WithContentService(s => s.GetContentAsync(
            new GetContentParameters { Id = id, IncludeUnpublished = true, Cached = false }, Ct))
        ?? throw new InvalidOperationException("Content not found.");

    /// <summary>A page or block as the editor shows it after a "Save only": with its draft's values, if it has one.</summary>
    private async Task<ContentItem> LoadAsDraftAsync(Guid id)
    {
        var content = await WithContentService(s => s.GetContentAsync(new GetContentParameters
        {
            Id = id, IncludeUnpublished = true, IncludeUnpublishedContent = true, Cached = false
        }, Ct)) ?? throw new InvalidOperationException("Content not found.");
        if (content.UnpublishedContent == null) return content;

        var draft = content.UnpublishedContent.JsonContent;
        if (content.IsNestedContent)
        {
            return draft.MapBlockDraftTo(content);
        }

        draft.MapDraftTo(content);
        content.PropertyData = draft.PropertyData;
        content.InvalidateContentValuesCache();
        return content;
    }

    /// <summary>What the front end shows: the page's live block list, published blocks only.</summary>
    private async Task<List<string?>> LiveBlockTextsAsync(Guid pageId)
    {
        var page = await LoadAsync(pageId);
        var blocks = await WithContentService(s => page.GetBlocks("blocks", s));
        return blocks.Select(b => b.GetValue<string>("text")).ToList();
    }

    private Task<Dictionary<Guid, ContentItem>> RowsAsync(params Guid[] ids) =>
        _site.QueryDatabaseAsync(db => db.Contents.AsNoTracking().Where(x => ids.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, Ct));

    private Task<string?> StoredValueAsync(Guid contentId, string alias) =>
        _site.QueryDatabaseAsync(db => db.ContentPropertyValues.AsNoTracking()
            .Where(x => x.ContentId == contentId && x.Alias == alias).Select(x => x.Value).SingleOrDefaultAsync(Ct));

    private Task<int> AuditCountAsync() => _site.QueryDatabaseAsync(db => db.Audits.CountAsync(Ct));

    private async Task<List<ContentVersion>> VersionsAsync(Guid contentId) =>
        (await WithVersioningService(s => s.GetContentVersionsAsync(
            new QueryContentVersionsParameters { ContentId = contentId }, Ct))).Items.ToList();

    private async Task<T> WithContentService<T>(Func<IContentService, Task<T>> call)
    {
        using var scope = _site.CreateScope();
        return await call(scope.ServiceProvider.GetRequiredService<IContentService>());
    }

    private async Task<T> WithVersioningService<T>(Func<IContentVersioningService, Task<T>> call)
    {
        using var scope = _site.CreateScope();
        return await call(scope.ServiceProvider.GetRequiredService<IContentVersioningService>());
    }
}

/// <summary>The same tests on a real PostgreSQL (Roadmap D15); skipped without Docker.</summary>
public class ContentTreeSaveTestsOnPostgreSql : ContentTreeSaveTests
{
    protected override string DatabaseProvider => TestSite.PostgreSqlProvider;
}

/// <summary>The same tests on SQL Server; skipped without <c>ZAUBER_TEST_SQLSERVER</c>.</summary>
public class ContentTreeSaveTestsOnSqlServer : ContentTreeSaveTests
{
    protected override string DatabaseProvider => TestSite.SqlServerProvider;
}
