using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Audit.Interfaces;
using ZauberCMS.Core.Audit.Parameters;
using ZauberCMS.Core.Content.Interfaces;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Content.Parameters;
using ZauberCMS.Core.Content.Services;
using ZauberCMS.Core.Data.Interfaces;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Languages.Interfaces;
using ZauberCMS.Core.Languages.Models;
using ZauberCMS.Core.Languages.Parameters;
using ZauberCMS.Core.Media.Interfaces;
using ZauberCMS.Core.Media.Models;
using ZauberCMS.Core.Media.Parameters;
using ZauberCMS.Core.Membership.Interfaces;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Membership.Parameters;
using ZauberCMS.Core.Shared.Models;
using ZauberCMS.Core.Shared.Services;
using ZauberCMS.Core.Tests.Infrastructure;
using AuditItem = ZauberCMS.Core.Audit.Models.Audit;
using ContentItemModel = ZauberCMS.Core.Content.Models.Content;
using MediaItem = ZauberCMS.Core.Media.Models.Media;

namespace ZauberCMS.Core.Tests.Shared;

/// <summary>
/// Roadmap Phase 10 (performance): the cached and slimmed-down queries still return what they did, and the caches
/// are cleared when what they depend on changes. Each test names the finding it guards.
/// </summary>
public class QueryAndCacheTests : IAsyncLifetime
{
    /// <summary>Overridden by the PostgreSQL and SQL Server copies of this class at the end of the file.</summary>
    protected virtual string DatabaseProvider => TestSite.SqliteProvider;

    private TestSite _site = null!;
    private ContentType _pageType = null!;

    public async ValueTask InitializeAsync()
    {
        _site = await TestSite.CreateAsync(databaseProvider: DatabaseProvider);
        _pageType = await _site.CreateContentTypeAsync("page");
    }

    public async ValueTask DisposeAsync() => await _site.DisposeAsync();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ZA061_The_query_string_does_not_change_which_page_is_served()
    {
        var page = await _site.CreateContentAsync(_pageType, "About");

        var plain = await GetEntryAsync("about", "http://example.test/about");
        var tracked = await GetEntryAsync("about", "http://example.test/about?utm_source=mail#top");

        Assert.Equal(page.Id, plain.Content?.Id);
        Assert.Equal(page.Id, tracked.Content?.Id);
    }

    [Fact]
    public async Task ZA187_Every_host_port_and_spelling_of_a_page_on_no_domain_shares_one_cache_entry()
    {
        var page = await _site.CreateContentAsync(_pageType, "About");

        // Host header values a visitor can make up, ports, and spellings of the path
        foreach (var (slug, url) in new[]
                 {
                     ("about", "http://example.test/about"),
                     ("about/", "http://other.test/about/"),
                     ("About", "https://EXAMPLE.test:8443/About"),
                     ("/about//", "http://made-up-1.test/about//"),
                     ("about", "http://made-up-2.test:1234/about?utm_source=mail")
                 })
        {
            Assert.Equal(page.Id, (await GetEntryAsync(slug, url)).Content?.Id);
        }

        Assert.Single(EntryCacheKeys());
    }

    [Fact]
    public async Task ZA187_A_domain_has_one_cache_entry_for_its_ports_and_host_spellings()
    {
        var first = await _site.CreateContentAsync(_pageType, "First");
        var second = await _site.CreateContentAsync(_pageType, "Second");
        var languageId = await AddLanguageAsync("de-DE");
        var saved = await WithService<IContentService, HandlerResult<Domain>>(s =>
            s.SaveDomainAsync(new SaveDomainParameters
            {
                Domain = new Domain { Url = "example.test", ContentId = second.Id, LanguageId = languageId }
            }, Ct));
        Assert.True(saved.Success, TestSite.Describe(saved.Messages));

        foreach (var url in new[] { "http://example.test/", "http://EXAMPLE.test:8080/", "https://example.test./" })
        {
            Assert.Equal(second.Id, (await GetRootAsync(url)).Content?.Id);
        }

        foreach (var url in new[] { "http://other.test/", "http://made-up.test:81/" })
        {
            Assert.Equal(first.Id, (await GetRootAsync(url)).Content?.Id);
        }

        // One for the domain, one for every host on no domain
        Assert.Equal(2, EntryCacheKeys().Count);
    }

    [Fact]
    public async Task ZA061_A_lookup_without_a_full_url_matches_no_domain_instead_of_failing()
    {
        var page = await _site.CreateContentAsync(_pageType, "About");

        var entry = await GetEntryAsync("about", url: null);

        Assert.Equal(page.Id, entry.Content?.Id);
    }

    [Fact]
    public async Task ZA061_Saving_a_domain_changes_the_cached_root_page_at_once()
    {
        var first = await _site.CreateContentAsync(_pageType, "First");
        var second = await _site.CreateContentAsync(_pageType, "Second");
        var languageId = await AddLanguageAsync("de-DE");

        var before = await GetRootAsync("http://example.test/");
        var saved = await WithService<IContentService, HandlerResult<Domain>>(s =>
            s.SaveDomainAsync(new SaveDomainParameters
            {
                Domain = new Domain { Url = "example.test", ContentId = second.Id, LanguageId = languageId }
            }, Ct));
        var after = await GetRootAsync("http://example.test/");

        Assert.True(saved.Success, TestSite.Describe(saved.Messages));
        Assert.Equal(first.Id, before.Content?.Id);
        Assert.Equal(second.Id, after.Content?.Id);
        Assert.Equal("de-DE", after.LanguageIsoCode);
    }

    [Fact]
    public async Task ZA061_Language_keys_follow_a_dictionary_change_without_a_content_save()
    {
        await _site.CreateContentAsync(_pageType, "About");
        var languageId = await AddLanguageAsync("en-US");
        await SetDefaultLanguageAsync("en-US");
        await AddDictionaryTextAsync(languageId, "greeting", "Hello");

        var before = await GetEntryAsync("about", "http://example.test/about");
        await _site.QueryDatabaseAsync(async db =>
        {
            var text = await db.LanguageTexts.SingleAsync(Ct);
            text.Value = "Hi";
            return await db.SaveChangesAsync(Ct);
        });
        // What a dictionary save through the admin clears
        _site.Services.GetRequiredService<ICacheService>()
            .ClearCachedItemsWithPrefix(nameof(LanguageDictionary));
        var after = await GetEntryAsync("about", "http://example.test/about");

        Assert.Equal("en-US", before.LanguageIsoCode);
        Assert.Equal("Hello", before.LanguageKeys["greeting"]);
        Assert.Equal("Hi", after.LanguageKeys["greeting"]);
    }

    [Fact]
    public async Task ZA061_A_miss_is_not_cached()
    {
        var before = await GetEntryAsync("late", "http://example.test/late");
        // Written straight to the database: no save clears the cache
        await _site.QueryDatabaseAsync(async db =>
        {
            db.Contents.Add(new ContentItemModel
            {
                Name = "Late", ContentTypeId = _pageType.Id, ContentTypeAlias = _pageType.Alias, Published = true,
                Url = "late"
            });
            return await db.SaveChangesAsync(Ct);
        });
        var after = await GetEntryAsync("late", "http://example.test/late");

        Assert.Null(before.Content);
        Assert.Equal("Late", after.Content?.Name);
    }

    [Fact]
    public async Task ZA061_Renaming_a_role_reaches_the_cached_page_at_once()
    {
        var roleId = await CreateRoleAsync("Members");
        var page = await _site.CreateContentAsync(_pageType, "Members area");
        Assert.True((await SetRolesAsync(page.Id, roleId)).Success);
        var before = await GetEntryAsync("members-area", "http://example.test/members-area");

        var role = await WithService<IMembershipService, Role?>(s => s.GetRoleAsync(new GetRoleParameters { Id = roleId }, Ct));
        role!.Name = "Subscribers";
        var saved = await WithService<IMembershipService, HandlerResult<Role>>(s =>
            s.SaveRoleAsync(new SaveRoleParameters { Role = role }, Ct));
        var after = await GetEntryAsync("members-area", "http://example.test/members-area");

        Assert.True(saved.Success, TestSite.Describe(saved.Messages));
        Assert.Equal("Members", Assert.Single(before.Content!.ContentRoles).Role.Name);
        Assert.Equal("Subscribers", Assert.Single(after.Content!.ContentRoles).Role.Name);
    }

    [Fact]
    public async Task ZA062_Querying_by_content_type_alias_filters_in_the_query()
    {
        var otherType = await _site.CreateContentTypeAsync("other");
        var page = await _site.CreateContentAsync(_pageType, "Page");
        await _site.CreateContentAsync(otherType, "Other");

        var pages = await QueryAsync(new QueryContentParameters { ContentTypeAlias = "page", Cached = true });
        var none = await QueryAsync(new QueryContentParameters { ContentTypeAlias = "missing" });
        // The alias wins over an id, as before
        var aliasWins = await QueryAsync(new QueryContentParameters
        {
            ContentTypeAlias = "page", ContentTypeId = otherType.Id
        });

        Assert.Equal([page.Id], pages.Select(x => x.Id));
        Assert.Empty(none);
        Assert.Equal([page.Id], aliasWins.Select(x => x.Id));
    }

    [Fact]
    public async Task ZA057_Paging_counts_without_and_with_a_full_first_page()
    {
        for (var i = 0; i < 3; i++)
        {
            await _site.CreateContentAsync(_pageType, $"Page {i}");
        }

        var all = await WithService<IContentService, PaginatedList<ContentItemModel>>(s =>
            s.QueryContentAsync(new QueryContentParameters { AmountPerPage = 10 }, Ct));
        var firstOfTwo = await WithService<IContentService, PaginatedList<ContentItemModel>>(s =>
            s.QueryContentAsync(new QueryContentParameters { AmountPerPage = 2 }, Ct));
        var secondOfTwo = await WithService<IContentService, PaginatedList<ContentItemModel>>(s =>
            s.QueryContentAsync(new QueryContentParameters { AmountPerPage = 2, PageIndex = 2 }, Ct));

        Assert.Equal((3, 1, 3), (all.TotalItems, all.TotalPages, all.Items.Count()));
        Assert.Equal((3, 2, 2), (firstOfTwo.TotalItems, firstOfTwo.TotalPages, firstOfTwo.Items.Count()));
        Assert.Equal((3, 2, 1), (secondOfTwo.TotalItems, secondOfTwo.TotalPages, secondOfTwo.Items.Count()));
    }

    [Fact]
    public async Task ZA064_Any_content_is_cached_and_follows_saves_and_the_recycle_bin()
    {
        var empty = await AnyContentAsync();
        var page = await _site.CreateContentAsync(_pageType, "Page");
        var one = await AnyContentAsync();
        var recycled = await WithService<IContentService, HandlerResult<ContentItemModel>>(s =>
            s.DeleteContentAsync(new DeleteContentParameters { ContentId = page.Id, MoveToRecycleBin = true }, Ct));
        var afterRecycle = await AnyContentAsync();

        Assert.True(recycled.Success, TestSite.Describe(recycled.Messages));
        Assert.Equal((false, true, false), (empty, one, afterRecycle));
    }

    [Fact]
    public async Task ZA056_A_role_loads_its_members_only_when_asked()
    {
        var user = await _site.CreateUserAsync("member");
        using (var scope = _site.CreateScope())
        {
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<Role>>();
            Assert.True((await roleManager.CreateAsync(new Role { Name = "Members" })).Succeeded);
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var added = await userManager.AddToRoleAsync(
                (await userManager.FindByIdAsync(user.Id.ToString()))!, "Members");
            Assert.True(added.Succeeded);
        }

        var roleId = await _site.QueryDatabaseAsync(db =>
            db.Roles.Where(x => x.Name == "Members").Select(x => x.Id).SingleAsync(Ct));

        var plain = await WithService<IMembershipService, Role?>(s =>
            s.GetRoleAsync(new GetRoleParameters { Id = roleId }, Ct));
        var withUsers = await WithService<IMembershipService, Role?>(s =>
            s.GetRoleAsync(new GetRoleParameters { Id = roleId, IncludeUsers = true }, Ct));
        var queried = await WithService<IMembershipService, PaginatedList<Role>>(s =>
            s.QueryRolesAsync(new QueryRolesParameters { Ids = [roleId] }, Ct));
        var queriedWithUsers = await WithService<IMembershipService, PaginatedList<Role>>(s =>
            s.QueryRolesAsync(new QueryRolesParameters { Ids = [roleId], IncludeUsers = true }, Ct));

        Assert.Empty(plain!.UserRoles);
        Assert.Equal(user.Id, Assert.Single(withUsers!.UserRoles).User.Id);
        Assert.Empty(Assert.Single(queried.Items).UserRoles);
        Assert.Single(Assert.Single(queriedWithUsers.Items).UserRoles);
    }

    [Fact]
    public async Task ZA071_All_dictionaries_load_every_language_with_an_empty_value_for_a_missing_text()
    {
        var english = await AddLanguageAsync("en-US");
        var german = await AddLanguageAsync("de-DE");
        await AddDictionaryTextAsync(english, "yes", "Yes");
        await AddDictionaryTextAsync(german, "yes", "Ja");
        await AddDictionaryTextAsync(english, "no", "No");

        var all = await WithService<ILanguageService, Dictionary<string, Dictionary<string, string>>>(s =>
            s.GetCachedAllLanguageDictionariesAsync(new GetCachedAllLanguageDictionariesParameters(), Ct));

        Assert.Equal("Yes", all["en-US"]["yes"]);
        Assert.Equal("No", all["en-US"]["no"]);
        Assert.Equal("Ja", all["de-DE"]["yes"]);
        Assert.Equal(string.Empty, all["de-DE"]["no"]);
    }

    [Fact]
    public async Task ZA083_Audit_cleanup_deletes_only_the_expired_rows()
    {
        await _site.QueryDatabaseAsync(async db =>
        {
            // Only what this test adds counts: start from no audits
            await db.Audits.ExecuteDeleteAsync(Ct);
            db.Audits.Add(new AuditItem { Description = "old", DateCreated = DateTime.UtcNow.AddDays(-91) });
            db.Audits.Add(new AuditItem { Description = "older", DateCreated = DateTime.UtcNow.AddDays(-400) });
            db.Audits.Add(new AuditItem { Description = "recent", DateCreated = DateTime.UtcNow.AddDays(-1) });
            return await db.SaveChangesAsync(Ct);
        });

        var result = await WithService<IAuditService, HandlerResult<int>>(s =>
            s.CleanupOldAuditsAsync(new CleanupOldAuditsParameters { DaysToKeep = 90 }, Ct));
        var left = await _site.QueryDatabaseAsync(db => db.Audits.Select(x => x.Description).ToListAsync(Ct));

        Assert.Equal(2, result.Entity);
        Assert.Equal(["recent"], left);
    }

    [Fact]
    public async Task ZA069_Pruning_keeps_the_newest_versions_the_published_one_and_the_latest_draft()
    {
        var page = await _site.CreateContentAsync(_pageType, "Page"); // version 1, published
        for (var i = 2; i <= 5; i++)
        {
            // "Save only": drafts, so version 1 stays the published one
            var draft = await LoadAsync(page.Id);
            draft.Name = $"Draft {i}";
            var saved = await WithService<IContentService, HandlerResult<ContentItemModel>>(s =>
                s.SaveContentAsync(new SaveContentParameters { Content = draft, SaveUnpublishedOnly = true }, Ct));
            Assert.True(saved.Success, TestSite.Describe(saved.Messages));
        }

        var versions = await VersionsAsync(page.Id);
        Assert.Equal([5, 4, 3, 2, 1], versions.Select(v => v.VersionNumber));
        // A kept version that names a removed one as its parent (no cascade on that key)
        var third = versions.Single(v => v.VersionNumber == 3).Id;
        await _site.QueryDatabaseAsync(db => db.ContentVersions.Where(v => v.VersionNumber == 5 && v.ContentId == page.Id)
            .ExecuteUpdateAsync(x => x.SetProperty(v => v.ParentVersionId, third), Ct));

        var pruned = await WithService<IContentVersioningService, HandlerResult<int>>(s =>
            s.PruneVersionsAsync(new PruneContentVersionsParameters { VersionsToKeep = 2 }, Ct));
        var left = await VersionsAsync(page.Id);

        Assert.Equal(2, pruned.Entity);
        Assert.Equal([5, 4, 1], left.Select(v => v.VersionNumber));
        Assert.True(left.Single(v => v.VersionNumber == 1).IsCurrentPublished);
        Assert.True(left.Single(v => v.VersionNumber == 5).IsLatestDraft);
        Assert.Null(left.Single(v => v.VersionNumber == 5).ParentVersionId);
    }

    [Fact]
    public async Task ZA069_Pruning_with_zero_keeps_everything()
    {
        var page = await _site.CreateContentAsync(_pageType, "Page");
        await WithService<IContentService, HandlerResult<ContentItemModel>>(async s =>
            await s.SaveContentAsync(new SaveContentParameters { Content = await LoadAsync(page.Id) }, Ct));

        var pruned = await WithService<IContentVersioningService, HandlerResult<int>>(s =>
            s.PruneVersionsAsync(new PruneContentVersionsParameters { VersionsToKeep = 0 }, Ct));

        Assert.Equal(0, pruned.Entity);
        Assert.Equal(2, (await VersionsAsync(page.Id)).Count);
    }

    [Fact]
    public async Task ZA069_The_version_list_can_leave_out_the_snapshots_and_never_loads_the_whole_user()
    {
        var user = await _site.CreateUserAsync("editor");
        _site.SignIn(user);
        var page = await _site.CreateContentAsync(_pageType, "Page");

        var full = Assert.Single(await VersionsAsync(page.Id));
        var light = Assert.Single((await WithService<IContentVersioningService, PaginatedList<ContentVersion>>(s =>
            s.GetContentVersionsAsync(new QueryContentVersionsParameters
            {
                ContentId = page.Id, IncludeSnapshots = false
            }, Ct))).Items);

        Assert.Equal("Page", full.Snapshot.Name);
        Assert.Null(light.Snapshot.Name);
        Assert.Empty(light.PropertySnapshots);
        Assert.Equal(full.Id, light.Id);
        Assert.Equal("editor", light.CreatedBy?.Name);
        Assert.Null(light.CreatedBy?.PasswordHash);
        Assert.Null(full.CreatedBy?.PasswordHash);
    }

    [Fact]
    public async Task ZA073_Public_access_is_set_on_the_page_and_every_page_below_it_in_one_change()
    {
        var roleId = await CreateRoleAsync("Members");
        var page = await _site.CreateContentAsync(_pageType, "Members area");
        var child = await _site.CreateContentAsync(_pageType, "Child", page.Id);
        var grandchild = await _site.CreateContentAsync(_pageType, "Grandchild", child.Id);
        var other = await _site.CreateContentAsync(_pageType, "Other");
        var before = await GetEntryAsync("grandchild", "http://example.test/grandchild"); // cached without roles
        var audits = await _site.QueryDatabaseAsync(db => db.Audits.CountAsync(Ct));
        var user = await _site.CreateUserAsync("editor");
        _site.SignIn(user);

        var set = await SetRolesAsync(page.Id, roleId);
        var restricted = await _site.QueryDatabaseAsync(db =>
            db.ContentRoles.Where(r => r.RoleId == roleId).Select(r => r.ContentId).ToListAsync(Ct));
        var after = await GetEntryAsync("grandchild", "http://example.test/grandchild");
        var auditsAfter = await _site.QueryDatabaseAsync(db => db.Audits.CountAsync(Ct));

        Assert.True(set.Success, TestSite.Describe(set.Messages));
        Assert.Equal(new[] { page.Id, child.Id, grandchild.Id }.Order(), restricted.Order());
        Assert.DoesNotContain(other.Id, restricted);
        Assert.Empty(before.Content!.ContentRoles);
        Assert.Single(after.Content!.ContentRoles);
        Assert.Equal(audits + 1, auditsAfter);

        var removed = await SetRolesAsync(page.Id);
        var left = await _site.QueryDatabaseAsync(db => db.ContentRoles.CountAsync(Ct));
        var afterRemove = await GetEntryAsync("grandchild", "http://example.test/grandchild");

        Assert.True(removed.Success, TestSite.Describe(removed.Messages));
        Assert.Equal(0, left);
        Assert.Empty(afterRemove.Content!.ContentRoles);
    }

    [Fact]
    public async Task ZA073_Public_access_below_a_restricted_ancestor_is_refused()
    {
        var roleId = await CreateRoleAsync("Members");
        var page = await _site.CreateContentAsync(_pageType, "Members area");
        var child = await _site.CreateContentAsync(_pageType, "Child", page.Id);
        Assert.True((await SetRolesAsync(page.Id, roleId)).Success);

        var refused = await SetRolesAsync(child.Id);
        var childRoles = await _site.QueryDatabaseAsync(db => db.ContentRoles.CountAsync(r => r.ContentId == child.Id, Ct));

        Assert.False(refused.Success);
        Assert.Equal(1, childRoles);
    }

    [Fact]
    public async Task ZA068_A_content_tree_level_comes_a_page_at_a_time_with_its_child_flags()
    {
        var root = await _site.CreateContentAsync(_pageType, "Root");
        var children = new List<ContentItemModel>();
        for (var i = 0; i < 5; i++)
        {
            children.Add(await _site.CreateContentAsync(_pageType, $"Child {i}", root.Id));
        }

        await _site.CreateContentAsync(_pageType, "Grandchild", children[1].Id);
        var recycled = await _site.CreateContentAsync(_pageType, "Recycled grandchild", children[3].Id);
        await SetDeletedAsync(recycled.Id);

        var rootLevel = await TreeLevelAsync(new ContentTreeLevelParameters());
        var first = await TreeLevelAsync(new ContentTreeLevelParameters { ParentId = root.Id, Take = 2 });
        var last = await TreeLevelAsync(new ContentTreeLevelParameters { ParentId = root.Id, Skip = 4, Take = 2 });
        var given = await TreeLevelAsync(new ContentTreeLevelParameters { Ids = [children[1].Id, children[2].Id], Take = 2 });

        Assert.Equal([root.Id], rootLevel.Items.Select(x => x.Id));
        Assert.Contains(root.Id, rootLevel.IdsWithChildren);
        Assert.Equal((5, 2, true), (first.TotalItems, first.Items.Count, first.HasMore));
        Assert.Equal(children.Take(2).Select(x => x.Id), first.Items.Select(x => x.Id));
        Assert.Equal([children[1].Id], first.IdsWithChildren);
        Assert.Equal((5, 1, false), (last.TotalItems, last.Items.Count, last.HasMore));
        // Only recycled children: no expand arrow
        Assert.DoesNotContain(children[3].Id, last.IdsWithChildren);
        Assert.Equal([children[1].Id], given.IdsWithChildren);
        Assert.All(first.Items, x => Assert.Empty(x.PropertyData));
        Assert.All(first.Items, x => Assert.NotNull(x.ContentType));
        var more = TreeLoadMore.After(first, root.Id);
        Assert.Equal((2, 5), (more!.Skip, more.TotalItems));
        Assert.Null(TreeLoadMore.After(last, root.Id));
    }

    [Fact]
    public async Task ZA068_A_media_tree_level_filters_by_type_in_the_items_and_the_flags()
    {
        var folder = new MediaItem { Name = "Folder", MediaType = MediaType.Folder };
        var imagesOnly = new MediaItem { Name = "Images", MediaType = MediaType.Folder };
        var docsOnly = new MediaItem { Name = "Docs", MediaType = MediaType.Folder };
        await _site.QueryDatabaseAsync(async db =>
        {
            db.Medias.AddRange(folder, imagesOnly, docsOnly);
            db.Medias.Add(new MediaItem { Name = "Top image", MediaType = MediaType.Image });
            db.Medias.Add(new MediaItem { Name = "Picture", MediaType = MediaType.Image, ParentId = imagesOnly.Id });
            db.Medias.Add(new MediaItem { Name = "Report", MediaType = MediaType.Document, ParentId = docsOnly.Id });
            db.Medias.Add(new MediaItem { Name = "Binned", MediaType = MediaType.Image, ParentId = folder.Id, Deleted = true });
            return await db.SaveChangesAsync(Ct);
        });

        var all = await MediaLevelAsync(new MediaTreeLevelParameters());
        var images = await MediaLevelAsync(new MediaTreeLevelParameters { MediaTypes = [MediaType.Image] });

        Assert.Equal(["Docs", "Folder", "Images", "Top image"], all.Items.Select(x => x.Name));
        Assert.Equal(new[] { imagesOnly.Id, docsOnly.Id }.Order(), all.IdsWithChildren.Order());
        Assert.Equal(["Docs", "Folder", "Images", "Top image"], images.Items.Select(x => x.Name));
        Assert.Equal([imagesOnly.Id], images.IdsWithChildren);
    }

    [Fact]
    public async Task ZA156_The_content_bin_pages_its_top_newest_first_and_opens_a_binned_page()
    {
        var live = await _site.CreateContentAsync(_pageType, "Live");
        var binnedBelowLive = await _site.CreateContentAsync(_pageType, "Binned below live", live.Id);
        var binnedPage = await _site.CreateContentAsync(_pageType, "Binned page");
        var child = await _site.CreateContentAsync(_pageType, "Child", binnedPage.Id);
        var grandchild = await _site.CreateContentAsync(_pageType, "Grandchild", child.Id);
        var alone = await _site.CreateContentAsync(_pageType, "Alone");
        // A block of the binned page, and one whose page is gone: blocks are never listed in a tree
        var block = await _site.CreateContentAsync(_pageType, "Block", binnedPage.Id);
        var orphanBlock = await _site.CreateContentAsync(_pageType, "Orphan block");
        await _site.QueryDatabaseAsync(async db =>
        {
            foreach (var row in await db.Contents.Where(x => x.Id == block.Id || x.Id == orphanBlock.Id).ToListAsync(Ct))
            {
                row.IsNestedContent = true;
                row.IsRootContent = false;
            }

            return await db.SaveChangesAsync(Ct);
        });
        var now = DateTime.UtcNow;
        await BinAsync(binnedBelowLive.Id, now.AddMinutes(-3));
        await BinAsync(binnedPage.Id, now.AddMinutes(-2));
        await BinAsync(child.Id, now.AddMinutes(-2));
        await BinAsync(grandchild.Id, now.AddMinutes(-2));
        await BinAsync(block.Id, now.AddMinutes(-2));
        await BinAsync(orphanBlock.Id, now);
        await BinAsync(alone.Id, now.AddMinutes(-1));

        var top = await TreeLevelAsync(new ContentTreeLevelParameters { InRecycleBin = true });
        var firstTwo = await TreeLevelAsync(new ContentTreeLevelParameters { InRecycleBin = true, Take = 2 });
        var below = await TreeLevelAsync(new ContentTreeLevelParameters { InRecycleBin = true, ParentId = binnedPage.Id });
        var liveRoots = await TreeLevelAsync(new ContentTreeLevelParameters());
        var liveChildren = await TreeLevelAsync(new ContentTreeLevelParameters { ParentId = live.Id });

        // What went in with its parent is below that parent, not at the top; a page binned from a live parent is
        Assert.Equal([alone.Id, binnedPage.Id, binnedBelowLive.Id], top.Items.Select(x => x.Id));
        Assert.Equal([binnedPage.Id], top.IdsWithChildren);
        Assert.Equal((3, true), (firstTwo.TotalItems, firstTwo.HasMore));
        Assert.Equal([child.Id], below.Items.Select(x => x.Id));
        Assert.Equal([child.Id], below.IdsWithChildren);
        // The live tree is unchanged: no binned page, and a live page whose only child is binned has no arrow
        Assert.Equal([live.Id], liveRoots.Items.Select(x => x.Id));
        Assert.DoesNotContain(live.Id, liveRoots.IdsWithChildren);
        Assert.Empty(liveChildren.Items);
    }

    [Fact]
    public async Task ZA156_The_media_bin_pages_its_top_newest_first_and_opens_a_binned_folder()
    {
        var now = DateTime.UtcNow;
        var liveFolder = new MediaItem { Name = "Live folder", MediaType = MediaType.Folder };
        var binnedFolder = new MediaItem
        {
            Name = "Binned folder", MediaType = MediaType.Folder, Deleted = true, DateUpdated = now.AddMinutes(-2)
        };
        var inBinnedFolder = new MediaItem
        {
            Name = "Inside", MediaType = MediaType.Image, ParentId = binnedFolder.Id, Deleted = true,
            DateUpdated = now.AddMinutes(-2)
        };
        var fromLiveFolder = new MediaItem
        {
            Name = "From live folder", MediaType = MediaType.Image, ParentId = liveFolder.Id, Deleted = true,
            DateUpdated = now.AddMinutes(-3)
        };
        var alone = new MediaItem
        {
            Name = "Alone", MediaType = MediaType.Document, Deleted = true, DateUpdated = now.AddMinutes(-1)
        };
        await _site.QueryDatabaseAsync(async db =>
        {
            db.Medias.AddRange(liveFolder, binnedFolder, inBinnedFolder, fromLiveFolder, alone);
            return await db.SaveChangesAsync(Ct);
        });

        var top = await MediaLevelAsync(new MediaTreeLevelParameters { InRecycleBin = true });
        var firstTwo = await MediaLevelAsync(new MediaTreeLevelParameters { InRecycleBin = true, Take = 2 });
        var inside = await MediaLevelAsync(new MediaTreeLevelParameters { InRecycleBin = true, ParentId = binnedFolder.Id });
        var liveTop = await MediaLevelAsync(new MediaTreeLevelParameters());

        Assert.Equal([alone.Id, binnedFolder.Id, fromLiveFolder.Id], top.Items.Select(x => x.Id));
        Assert.Equal([binnedFolder.Id], top.IdsWithChildren);
        Assert.Equal((3, true), (firstTwo.TotalItems, firstTwo.HasMore));
        Assert.Equal([inBinnedFolder.Id], inside.Items.Select(x => x.Id));
        Assert.Equal([liveFolder.Id], liveTop.Items.Select(x => x.Id));
        Assert.Empty(liveTop.IdsWithChildren);
    }

    private Task BinAsync(Guid id, DateTime binnedAt) =>
        _site.QueryDatabaseAsync(async db =>
        {
            var row = await db.Contents.SingleAsync(x => x.Id == id, Ct);
            row.Deleted = true;
            row.DateUpdated = binnedAt;
            return await db.SaveChangesAsync(Ct);
        });

    [Fact]
    public async Task ZA082_Async_paging_pages_an_in_memory_query_too()
    {
        var page = await Enumerable.Range(1, 5).AsQueryable().ToPaginatedListAsync(2, 2, Ct);

        Assert.Equal([3, 4], page.Items);
        Assert.Equal((5, 3), (page.TotalItems, page.TotalPages));
    }

    // ---- helpers ----

    private async Task<TResult> WithService<TService, TResult>(Func<TService, Task<TResult>> call)
        where TService : notnull
    {
        using var scope = _site.CreateScope();
        return await call(scope.ServiceProvider.GetRequiredService<TService>());
    }

    private async Task<ContentItemModel> LoadAsync(Guid id) =>
        await WithService<IContentService, ContentItemModel?>(s => s.GetContentAsync(new GetContentParameters
        {
            Id = id, IncludeUnpublished = true
        }, Ct)) ?? throw new InvalidOperationException("Content not found.");

    private async Task<List<ContentVersion>> VersionsAsync(Guid contentId) =>
        (await WithService<IContentVersioningService, PaginatedList<ContentVersion>>(s =>
            s.GetContentVersionsAsync(new QueryContentVersionsParameters { ContentId = contentId }, Ct))).Items.ToList();

    private Task<HandlerResult<ContentItemModel>> SetRolesAsync(Guid contentId, params Guid[] roleIds) =>
        WithService<IContentService, HandlerResult<ContentItemModel>>(s => s.SetContentRolesAsync(
            new SetContentRolesParameters { ContentId = contentId, RoleIds = roleIds.ToList() }, Ct));

    private async Task<Guid> CreateRoleAsync(string name)
    {
        using var scope = _site.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<Role>>();
        var role = new Role { Name = name };
        Assert.True((await roleManager.CreateAsync(role)).Succeeded);
        return role.Id;
    }

    private Task<TreeLevel<ContentItemModel>> TreeLevelAsync(ContentTreeLevelParameters parameters) =>
        WithService<IContentService, TreeLevel<ContentItemModel>>(s => s.GetContentTreeLevelAsync(parameters, Ct));

    private Task<TreeLevel<MediaItem>> MediaLevelAsync(MediaTreeLevelParameters parameters) =>
        WithService<IMediaService, TreeLevel<MediaItem>>(s => s.GetMediaTreeLevelAsync(parameters, Ct));

    private Task SetDeletedAsync(Guid id) =>
        _site.QueryDatabaseAsync(async db =>
        {
            var row = await db.Contents.SingleAsync(x => x.Id == id, Ct);
            row.Deleted = true;
            return await db.SaveChangesAsync(Ct);
        });

    private Task<EntryModel> GetEntryAsync(string slug, string? url) =>
        WithService<IContentService, EntryModel>(s => s.GetContentFromRequestAsync(
            new GetContentFromRequestParameters { Slug = slug, Url = url }, Ct));

    /// <summary>
    /// The URL lookups cached now (ZA-187), without their 5-minute window, so a test that runs across the change of
    /// window does not count one lookup twice.
    /// </summary>
    private List<string> EntryCacheKeys()
    {
        var prefix = ContentService.EntryCacheKeyPrefix.GenerateCacheKey<ContentItemModel>();
        return _site.Services.GetRequiredService<ServerCache>().Keys
            .Where(k => k.StartsWith(prefix, StringComparison.Ordinal))
            .Select(k => string.Concat(k[prefix.Length..].SkipWhile(char.IsDigit)))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private Task<EntryModel> GetRootAsync(string url) =>
        WithService<IContentService, EntryModel>(s => s.GetContentFromRequestAsync(
            new GetContentFromRequestParameters { IsRootContent = true, Url = url }, Ct));

    private async Task<List<ContentItemModel>> QueryAsync(QueryContentParameters parameters) =>
        (await WithService<IContentService, PaginatedList<ContentItemModel>>(s =>
            s.QueryContentAsync(parameters, Ct))).Items.ToList();

    private Task<bool> AnyContentAsync() => WithService<IContentService, bool>(s => s.AnyContentAsync(Ct));

    private Task<Guid> AddLanguageAsync(string isoCode) =>
        _site.QueryDatabaseAsync(async db =>
        {
            var language = new Language { LanguageIsoCode = isoCode, LanguageCultureName = isoCode };
            db.Languages.Add(language);
            await db.SaveChangesAsync(Ct);
            return language.Id;
        });

    private Task AddDictionaryTextAsync(Guid languageId, string key, string value) =>
        _site.QueryDatabaseAsync(async db =>
        {
            var dictionary = await db.LanguageDictionaries.FirstOrDefaultAsync(x => x.Key == key, Ct);
            if (dictionary == null)
            {
                dictionary = new LanguageDictionary { Key = key };
                db.LanguageDictionaries.Add(dictionary);
            }

            db.LanguageTexts.Add(new LanguageText
            {
                LanguageDictionaryId = dictionary.Id, LanguageId = languageId, Value = value
            });
            return await db.SaveChangesAsync(Ct);
        });

    private async Task SetDefaultLanguageAsync(string isoCode)
    {
        using var scope = _site.CreateScope();
        var dataService = scope.ServiceProvider.GetRequiredService<IDataService>();
        var settings = await dataService.GetGlobalSettings();
        settings.DefaultLanguage = isoCode;
        var saved = await dataService.SaveGlobalSettings(settings);
        Assert.True(saved, "Saving the global settings failed.");
    }
}

/// <summary>The same tests on a real PostgreSQL (Roadmap D15); skipped without Docker.</summary>
public class QueryAndCacheTestsOnPostgreSql : QueryAndCacheTests
{
    protected override string DatabaseProvider => TestSite.PostgreSqlProvider;
}

/// <summary>The same tests on SQL Server; skipped without <c>ZAUBER_TEST_SQLSERVER</c>.</summary>
public class QueryAndCacheTestsOnSqlServer : QueryAndCacheTests
{
    protected override string DatabaseProvider => TestSite.SqlServerProvider;
}
