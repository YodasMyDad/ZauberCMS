using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Content.Interfaces;
using ZauberCMS.Core.Content.Mapping;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Content.Parameters;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Languages.Models;
using ZauberCMS.Core.Shared.Models;
using ZauberCMS.Core.Tests.Infrastructure;
using ContentItem = ZauberCMS.Core.Content.Models.Content;

namespace ZauberCMS.Core.Tests.Content;

/// <summary>
/// Roadmap Phase 5 (content integrity): URLs, drafts, the recycle bin, copy, move, versions and what the front end
/// serves. Each test names the finding it guards.
/// </summary>
public class ContentIntegrityTests : IAsyncLifetime
{
    private TestSite _site = null!;
    private ContentType _pageType = null!;

    public async ValueTask InitializeAsync()
    {
        _site = await TestSite.CreateAsync();
        _pageType = await _site.CreateContentTypeAsync("page");
    }

    public async ValueTask DisposeAsync() => await _site.DisposeAsync();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // ---- ZA-046: duplicate URLs ----

    [Fact]
    public async Task ZA046_A_duplicate_url_on_language_content_does_not_break_the_front_end()
    {
        var first = await _site.CreateContentAsync(_pageType, "First");
        var second = await _site.CreateContentAsync(_pageType, "Second");
        var languageId = await AddLanguageAsync("de-DE");
        // Older data can hold duplicates; the save path no longer creates them
        await _site.QueryDatabaseAsync(async db =>
        {
            foreach (var page in await db.Contents.Where(x => x.Id == first.Id || x.Id == second.Id).ToListAsync(Ct))
            {
#pragma warning disable CS0618
                page.Url = "same";
#pragma warning restore CS0618
                page.LanguageId = languageId;
            }

            return await db.SaveChangesAsync(Ct);
        });

        var languages = await WithContentService(s => s.GetContentLanguagesAsync(new GetContentLanguagesParameters(), Ct));
        var entry = await GetEntryAsync("same");

        Assert.Equal("de-DE", languages["same"]);
        Assert.NotNull(entry.Content);
    }

    [Fact]
    public async Task ZA046_A_typed_url_that_another_page_has_is_refused()
    {
        await _site.CreateContentAsync(_pageType, "About");
        var other = await LoadAsync((await _site.CreateContentAsync(_pageType, "Contact")).Id);
#pragma warning disable CS0618
        other.Url = "About";
#pragma warning restore CS0618

        var result = await SaveAsync(new SaveContentParameters { Content = other });

        Assert.False(result.Success);
        Assert.Equal("contact", await UrlOfAsync(other.Id));
    }

    [Fact]
    public async Task ZA046_Copying_a_page_twice_gives_each_copy_its_own_url()
    {
        var page = await _site.CreateContentAsync(_pageType, "News");

        await CopyAsync(page.Id);
        await CopyAsync(page.Id);

        var urls = await _site.QueryDatabaseAsync(db => db.Contents.AsNoTracking()
            .Select(x => x.Url()).ToListAsync(Ct));
        Assert.Equal(3, urls.Count);
        Assert.Equal(3, urls.Distinct().Count());
    }

    [Fact]
    public async Task ZA046_Restoring_a_page_whose_url_was_taken_meanwhile_gives_it_a_new_one()
    {
        var page = await _site.CreateContentAsync(_pageType, "Offers");
        await RecycleAsync(page.Id);
        var newer = await _site.CreateContentAsync(_pageType, "Offers");

        var result = await RestoreAsync(page.Id);

        Assert.True(result.Success, TestSite.Describe(result.Messages));
        Assert.Contains(result.Messages, m => m.MessageType == ResultMessageType.Warning);
        Assert.Equal("offers", await UrlOfAsync(newer.Id));
        Assert.NotEqual("offers", await UrlOfAsync(page.Id));
    }

    // ---- ZA-047: drafts ----

    [Fact]
    public async Task ZA047_A_save_without_property_data_keeps_the_pending_draft()
    {
        var page = await _site.CreateContentAsync(_pageType, "Home");
        await SaveDraftAsync(page.Id, "Home (draft)");

        var live = await LoadAsync(page.Id);
        live.SortOrder = 5;
        var sorted = await SaveAsync(new SaveContentParameters { Content = live, ExcludePropertyData = true });

        Assert.True(sorted.Success, TestSite.Describe(sorted.Messages));
        var stored = await StoredAsync(page.Id);
        Assert.NotNull(stored.UnpublishedContentId);
        Assert.True(await _site.QueryDatabaseAsync(db => db.UnpublishedContent.AnyAsync(Ct)));
        Assert.Equal(5, stored.SortOrder);
    }

    [Fact]
    public async Task ZA047_A_metadata_save_of_a_copy_loaded_before_the_draft_keeps_the_draft_link()
    {
        var page = await _site.CreateContentAsync(_pageType, "Home");
        var loadedBeforeDraft = await LoadAsync(page.Id);
        await SaveDraftAsync(page.Id, "Home (draft)");

        loadedBeforeDraft.SortOrder = 3;
        var sorted = await SaveAsync(new SaveContentParameters { Content = loadedBeforeDraft, ExcludePropertyData = true });

        Assert.True(sorted.Success, TestSite.Describe(sorted.Messages));
        Assert.NotNull((await StoredAsync(page.Id)).UnpublishedContentId);
    }

    [Fact]
    public async Task ZA047_Publishing_with_property_data_replaces_the_draft()
    {
        var page = await _site.CreateContentAsync(_pageType, "Home");
        await SaveDraftAsync(page.Id, "Home (draft)");

        var published = await SaveAsync(new SaveContentParameters { Content = await LoadAsync(page.Id) });

        Assert.True(published.Success, TestSite.Describe(published.Messages));
        Assert.Null((await StoredAsync(page.Id)).UnpublishedContentId);
        Assert.False(await _site.QueryDatabaseAsync(db => db.UnpublishedContent.AnyAsync(Ct)));
    }

    [Fact]
    public void ZA047_A_draft_shown_in_the_editor_keeps_the_live_tree_position()
    {
        var parentId = Guid.NewGuid();
        var live = new ContentItem { Name = "Live", ParentId = parentId, SortOrder = 7, Path = [parentId] };
        var draft = new ContentItem { Name = "Draft", ParentId = null, SortOrder = 1, Path = [], HideFromNavigation = true };

        draft.MapDraftTo(live);

        Assert.Equal("Draft", live.Name);
        Assert.True(live.HideFromNavigation);
        Assert.Equal(parentId, live.ParentId);
        Assert.Equal(7, live.SortOrder);
        Assert.Equal([parentId], live.Path);
    }

    // ---- ZA-048: recycle bin with children ----

    [Fact]
    public async Task ZA048_Recycling_a_parent_recycles_everything_below_it()
    {
        var parent = await _site.CreateContentAsync(_pageType, "Parent");
        var child = await _site.CreateContentAsync(_pageType, "Child", parent.Id);
        var grandchild = await _site.CreateContentAsync(_pageType, "Grandchild", child.Id);

        var result = await RecycleAsync(parent.Id);

        Assert.True(result.Success, TestSite.Describe(result.Messages));
        Assert.True((await StoredAsync(child.Id)).Deleted);
        Assert.True((await StoredAsync(grandchild.Id)).Deleted);
        Assert.Null((await GetEntryAsync("grandchild")).Content);
    }

    [Fact]
    public async Task ZA048_Restoring_a_parent_restores_what_went_to_the_bin_with_it()
    {
        var parent = await _site.CreateContentAsync(_pageType, "Parent");
        var child = await _site.CreateContentAsync(_pageType, "Child", parent.Id);
        await RecycleAsync(parent.Id);

        var result = await RestoreAsync(parent.Id);

        Assert.True(result.Success, TestSite.Describe(result.Messages));
        Assert.False((await StoredAsync(parent.Id)).Deleted);
        Assert.False((await StoredAsync(child.Id)).Deleted);
    }

    [Fact]
    public async Task ZA048_Restoring_a_parent_leaves_a_child_that_was_binned_on_its_own_in_the_bin()
    {
        var parent = await _site.CreateContentAsync(_pageType, "Parent");
        var keptChild = await _site.CreateContentAsync(_pageType, "Kept", parent.Id);
        var binnedEarlier = await _site.CreateContentAsync(_pageType, "Binned earlier", parent.Id);
        await RecycleAsync(binnedEarlier.Id);
        await RecycleAsync(parent.Id);

        var result = await RestoreAsync(parent.Id);

        Assert.True(result.Success, TestSite.Describe(result.Messages));
        Assert.False((await StoredAsync(keptChild.Id)).Deleted);
        Assert.True((await StoredAsync(binnedEarlier.Id)).Deleted);
    }

    [Fact]
    public async Task ZA048_A_page_is_not_restored_under_a_parent_that_is_still_in_the_bin()
    {
        var parent = await _site.CreateContentAsync(_pageType, "Parent");
        var child = await _site.CreateContentAsync(_pageType, "Child", parent.Id);
        await RecycleAsync(parent.Id);

        var result = await RestoreAsync(child.Id);

        Assert.False(result.Success);
        Assert.True((await StoredAsync(child.Id)).Deleted);
    }

    [Fact]
    public async Task ZA048_Permanently_deleting_a_recycled_parent_deletes_its_recycled_children()
    {
        var parent = await _site.CreateContentAsync(_pageType, "Parent");
        var child = await _site.CreateContentAsync(_pageType, "Child", parent.Id);
        await _site.CreateContentAsync(_pageType, "Grandchild", child.Id);
        await RecycleAsync(parent.Id);

        var result = await DeleteAsync(parent.Id, moveToRecycleBin: false);

        Assert.True(result.Success, TestSite.Describe(result.Messages));
        Assert.False(await _site.QueryDatabaseAsync(db => db.Contents.AnyAsync(Ct)));
    }

    // ---- ZA-049: copy and drafts ----

    [Fact]
    public async Task ZA049_A_copy_does_not_share_the_original_draft()
    {
        var page = await _site.CreateContentAsync(_pageType, "Home");
        await SaveDraftAsync(page.Id, "Home (draft)");

        await CopyAsync(page.Id);
        var copy = await _site.QueryDatabaseAsync(db => db.Contents.AsNoTracking().SingleAsync(x => x.Id != page.Id, Ct));
        var published = await SaveAsync(new SaveContentParameters { Content = await LoadAsync(page.Id) });

        Assert.Null(copy.UnpublishedContentId);
        Assert.True(published.Success, TestSite.Describe(published.Messages));
    }

    [Fact]
    public async Task ZA049_Copy_with_descendants_copies_the_live_ones_with_correct_paths()
    {
        var parent = await _site.CreateContentAsync(_pageType, "Parent");
        var child = await _site.CreateContentAsync(_pageType, "Child", parent.Id);
        var binned = await _site.CreateContentAsync(_pageType, "Binned", parent.Id);
        await RecycleAsync(binned.Id);

        await WithContentService(s => s.CopyContentAsync(
            new CopyContentParameters { ContentToCopy = parent.Id, IncludeDescendants = true }, Ct));

        var all = await _site.QueryDatabaseAsync(db => db.Contents.AsNoTracking().ToListAsync(Ct));
        var parentCopy = all.Single(x => x.Name == "Parent (Copy)");
        var childCopy = all.Single(x => x.Name == "Child (Copy)");
        Assert.DoesNotContain(all, x => x.Name == "Binned (Copy)");
        Assert.Equal(parentCopy.Id, childCopy.ParentId);
        Assert.Equal([parentCopy.Id, childCopy.Id], childCopy.Path);
        Assert.NotEqual(child.Url(), childCopy.Url());
    }

    // ---- ZA-065 / ZA-066 / ZA-067: what the front end serves ----

    [Fact]
    public async Task ZA065_Unpublished_and_recycled_children_are_not_served()
    {
        var parent = await _site.CreateContentAsync(_pageType, "Parent");
        var live = await _site.CreateContentAsync(_pageType, "Live", parent.Id);
        var hidden = await _site.CreateContentAsync(_pageType, "Hidden", parent.Id);
        var binned = await _site.CreateContentAsync(_pageType, "Binned", parent.Id);
        await SetAsync(hidden.Id, c => c.Published = false);
        await RecycleAsync(binned.Id);

        var entry = await WithContentService(s => s.GetContentFromRequestAsync(new GetContentFromRequestParameters
        {
            Slug = "parent", Url = "http://localhost/parent", IncludeChildren = true
        }, Ct));

        Assert.NotNull(entry.Content);
        Assert.Equal([live.Id], entry.Content.Children.Select(x => x.Id));
    }

    [Fact]
    public async Task ZA065_An_internal_redirect_to_an_unpublished_page_is_not_followed()
    {
        var target = await _site.CreateContentAsync(_pageType, "Target");
        var redirect = await _site.CreateContentAsync(_pageType, "Redirect");
        await SetAsync(target.Id, c => c.Published = false);
        await SetAsync(redirect.Id, c => c.InternalRedirectId = target.Id);

        var entry = await GetEntryAsync("redirect");

        Assert.Null(entry.Content);
    }

    [Fact]
    public async Task ZA065_An_unpublished_domain_root_is_not_served()
    {
        var root = await _site.CreateContentAsync(_pageType, "Root");
        var languageId = await AddLanguageAsync("en-GB");
        await _site.QueryDatabaseAsync(async db =>
        {
            db.Domains.Add(new Domain { Url = "example.test", ContentId = root.Id, LanguageId = languageId });
            return await db.SaveChangesAsync(Ct);
        });
        await SetAsync(root.Id, c => c.Published = false);

        var entry = await WithContentService(s => s.GetContentFromRequestAsync(new GetContentFromRequestParameters
        {
            IsRootContent = true, Url = "http://example.test/"
        }, Ct));

        Assert.Null(entry.Content);
    }

    [Theory]
    [InlineData("About-Us")]
    [InlineData("/about-us/")]
    [InlineData("ABOUT-US/")]
    public async Task ZA066_The_slug_matches_whatever_its_case_and_slashes(string slug)
    {
        var page = await _site.CreateContentAsync(_pageType, "About Us");

        var entry = await GetEntryAsync(slug);

        Assert.Equal(page.Id, entry.Content?.Id);
    }

    [Fact]
    public async Task ZA067_A_block_gets_no_url_and_does_not_take_a_page_slug()
    {
        var blockType = await _site.CreateContentTypeAsync("hero", isElementType: true);
        var block = await WithContentService(s => s.SaveContentAsync(new SaveContentParameters
        {
            Content = new ContentItem
            {
                Name = "Hero", ContentTypeId = blockType.Id, ContentTypeAlias = blockType.Alias,
                IsNestedContent = true, Published = true
            }
        }, Ct));
        Assert.True(block.Success, TestSite.Describe(block.Messages));

        var page = await _site.CreateContentAsync(_pageType, "Hero");

        Assert.Null(await UrlOfAsync(block.Entity!.Id));
        Assert.Equal("hero", await UrlOfAsync(page.Id));
        Assert.Equal(page.Id, (await GetEntryAsync("hero")).Content?.Id);
    }

    [Fact]
    public async Task ZA067_A_block_with_an_old_url_is_not_served_as_a_page()
    {
        var blockType = await _site.CreateContentTypeAsync("hero", isElementType: true);
        var block = await _site.CreateContentAsync(blockType, "Old block");
        await SetAsync(block.Id, c => c.IsNestedContent = true);

        Assert.Null((await GetEntryAsync("old-block")).Content);
    }

    // ---- ZA-070: deleting a content type ----

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ZA070_A_content_type_used_by_unpublished_or_recycled_content_is_not_deleted(bool recycled)
    {
        var otherType = await _site.CreateContentTypeAsync("article");
        var page = await _site.CreateContentAsync(otherType, "Article");
        if (recycled)
        {
            await RecycleAsync(page.Id);
        }
        else
        {
            await SetAsync(page.Id, c => c.Published = false);
        }

        var result = await WithContentService(s => s.DeleteContentTypeAsync(
            new DeleteContentTypeParameters { ContentTypeId = otherType.Id }, Ct));

        Assert.False(result.Success);
        Assert.True(await _site.QueryDatabaseAsync(db => db.Contents.AnyAsync(x => x.Id == page.Id, Ct)));
    }

    // ---- ZA-074 / ZA-075: versions ----

    [Fact]
    public async Task ZA074_A_draft_version_records_the_draft_not_the_live_values()
    {
        var page = await _site.CreateContentAsync(_pageType, "Live name");

        await SaveDraftAsync(page.Id, "Draft name");

        var versions = await VersionsAsync(page.Id);
        var draftVersion = versions.First(v => v.Status == ContentVersionStatus.Draft);
        Assert.Equal("Draft name", draftVersion.Snapshot.Name);
    }

    [Fact]
    public async Task ZA075_Restoring_a_version_keeps_tree_position_and_publish_state_and_drops_the_draft()
    {
        var parent = await _site.CreateContentAsync(_pageType, "Parent");
        var page = await _site.CreateContentAsync(_pageType, "Original");
        var firstVersion = (await VersionsAsync(page.Id)).Single();

        // Moved and renamed since, and a draft pending
        var moved = await LoadAsync(page.Id);
        moved.ParentId = parent.Id;
        moved.IsRootContent = false;
        moved.Name = "Renamed";
        Assert.True((await SaveAsync(new SaveContentParameters { Content = moved })).Success);
        await SaveDraftAsync(page.Id, "Draft");

        var result = await WithVersioningService(s => s.PublishVersionAsync(
            new PublishContentVersionParameters { VersionId = firstVersion.Id, PublishedByUserId = _site.CurrentUser.Id }, Ct));

        Assert.True(result.Success, TestSite.Describe(result.Messages));
        var stored = await StoredAsync(page.Id);
        Assert.Equal("Original", stored.Name);
        Assert.Equal(parent.Id, stored.ParentId);
        Assert.Equal([parent.Id, page.Id], stored.Path);
        Assert.True(stored.Published);
        Assert.Null(stored.UnpublishedContentId);
        Assert.True(await _site.QueryDatabaseAsync(db => db.Audits.AnyAsync(a => a.Description!.Contains("Restored"), Ct)));
    }

    [Fact]
    public async Task ZA075_The_item_a_publish_returns_can_be_saved_again()
    {
        var page = await _site.CreateContentAsync(_pageType, "Home");
        var copy = await LoadAsync(page.Id);
        copy.Name = "Saved";
        var first = await SaveAsync(new SaveContentParameters { Content = copy });
        Assert.True(first.Success, TestSite.Describe(first.Messages));

        // The published version used to be written back onto the row, changing DateUpdated (the concurrency
        // token) after the save had returned, so saving the returned item again was refused as outdated
        first.Entity!.Name = "Saved again";
        var second = await SaveAsync(new SaveContentParameters { Content = first.Entity });

        Assert.True(second.Success, TestSite.Describe(second.Messages));
    }

    // ---- ZA-079: move ----

    [Fact]
    public async Task ZA079_Moving_a_page_rewrites_the_paths_below_it()
    {
        var a = await _site.CreateContentAsync(_pageType, "A");
        var b = await _site.CreateContentAsync(_pageType, "B");
        var child = await _site.CreateContentAsync(_pageType, "Child", a.Id);
        var grandchild = await _site.CreateContentAsync(_pageType, "Grandchild", child.Id);

        var moving = await LoadAsync(child.Id);
        moving.ParentId = b.Id;
        var result = await SaveAsync(new SaveContentParameters { Content = moving, ExcludePropertyData = true });

        Assert.True(result.Success, TestSite.Describe(result.Messages));
        Assert.Equal([b.Id, child.Id], (await StoredAsync(child.Id)).Path);
        Assert.Equal([b.Id, child.Id, grandchild.Id], (await StoredAsync(grandchild.Id)).Path);
    }

    [Fact]
    public async Task ZA079_A_page_cannot_be_moved_below_its_own_descendant()
    {
        var parent = await _site.CreateContentAsync(_pageType, "Parent");
        var child = await _site.CreateContentAsync(_pageType, "Child", parent.Id);

        var moving = await LoadAsync(parent.Id);
        moving.ParentId = child.Id;
        moving.IsRootContent = false;
        var result = await SaveAsync(new SaveContentParameters { Content = moving, ExcludePropertyData = true });

        Assert.False(result.Success);
        Assert.Null((await StoredAsync(parent.Id)).ParentId);
    }

    // ---- ZA-016: the recycle-bin filter on queries ----

    [Fact]
    public async Task ZA016_IsDeleted_null_returns_both_and_included_children_never_hold_recycled_items()
    {
        var parent = await _site.CreateContentAsync(_pageType, "Parent");
        var live = await _site.CreateContentAsync(_pageType, "Live", parent.Id);
        var binned = await _site.CreateContentAsync(_pageType, "Binned", parent.Id);
        await RecycleAsync(binned.Id);

        var both = await WithContentService(s => s.QueryContentAsync(new QueryContentParameters
        {
            IsDeleted = null, IncludeUnpublished = true, AmountPerPage = 50
        }, Ct));
        var withChildren = await WithContentService(s => s.QueryContentAsync(new QueryContentParameters
        {
            Ids = [parent.Id], IncludeChildren = true, IncludeUnpublished = true
        }, Ct));

        Assert.Contains(both.Items, x => x.Id == binned.Id);
        Assert.Contains(both.Items, x => x.Id == live.Id);
        Assert.Equal([live.Id], withChildren.Items.Single().Children.Select(x => x.Id));
    }

    // ---- helpers ----

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

    private Task<HandlerResult<ContentItem>> SaveAsync(SaveContentParameters parameters) =>
        WithContentService(s => s.SaveContentAsync(parameters, Ct));

    private async Task<ContentItem> LoadAsync(Guid id) =>
        await WithContentService(s => s.GetContentAsync(new GetContentParameters
        {
            Id = id, IncludeUnpublished = true, IncludeDeleted = true, Cached = false
        }, Ct)) ?? throw new InvalidOperationException("Content not found.");

    private Task<ContentItem> StoredAsync(Guid id) =>
        _site.QueryDatabaseAsync(db => db.Contents.AsNoTracking().SingleAsync(x => x.Id == id, Ct));

    private Task<string?> UrlOfAsync(Guid id) =>
        _site.QueryDatabaseAsync(async db => (await db.Contents.AsNoTracking().SingleAsync(x => x.Id == id, Ct)).Url());

    private async Task SaveDraftAsync(Guid id, string name)
    {
        var draft = await LoadAsync(id);
        draft.Name = name;
        var result = await SaveAsync(new SaveContentParameters { Content = draft, SaveUnpublishedOnly = true });
        Assert.True(result.Success, TestSite.Describe(result.Messages));
    }

    private Task<HandlerResult<ContentItem>> RecycleAsync(Guid id) => DeleteAsync(id, moveToRecycleBin: true);

    private Task<HandlerResult<ContentItem>> DeleteAsync(Guid id, bool moveToRecycleBin) =>
        WithContentService(s => s.DeleteContentAsync(
            new DeleteContentParameters { ContentId = id, MoveToRecycleBin = moveToRecycleBin }, Ct));

    // What the recycle-bin "Restore" menu does
    private async Task<HandlerResult<ContentItem>> RestoreAsync(Guid id)
    {
        var recycled = await LoadAsync(id);
        recycled.Deleted = false;
        return await SaveAsync(new SaveContentParameters { Content = recycled, ExcludePropertyData = true });
    }

    private Task<HandlerResult<ContentItem>> CopyAsync(Guid id) =>
        WithContentService(s => s.CopyContentAsync(new CopyContentParameters { ContentToCopy = id }, Ct));

    private Task<EntryModel> GetEntryAsync(string slug) =>
        WithContentService(s => s.GetContentFromRequestAsync(new GetContentFromRequestParameters
        {
            Slug = slug, Url = $"http://localhost/{slug.Trim('/')}"
        }, Ct));

    private async Task<List<ContentVersion>> VersionsAsync(Guid contentId) =>
        (await WithVersioningService(s => s.GetContentVersionsAsync(
            new QueryContentVersionsParameters { ContentId = contentId }, Ct))).Items.ToList();

    /// <summary>Changes a stored row directly, for states the admin reaches through several steps.</summary>
    private Task SetAsync(Guid id, Action<ContentItem> change) =>
        _site.QueryDatabaseAsync(async db =>
        {
            var row = await db.Contents.SingleAsync(x => x.Id == id, Ct);
            change(row);
            return await db.SaveChangesAsync(Ct);
        });

    private Task<Guid> AddLanguageAsync(string isoCode) =>
        _site.QueryDatabaseAsync(async db =>
        {
            var language = new Language { LanguageIsoCode = isoCode, LanguageCultureName = isoCode };
            db.Languages.Add(language);
            await db.SaveChangesAsync(Ct);
            return language.Id;
        });
}
