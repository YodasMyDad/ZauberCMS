using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Audit.Interfaces;
using ZauberCMS.Core.Audit.Parameters;
using ZauberCMS.Core.Content.Interfaces;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Content.Parameters;
using ZauberCMS.Core.Data.Interfaces;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Languages.Interfaces;
using ZauberCMS.Core.Languages.Models;
using ZauberCMS.Core.Languages.Parameters;
using ZauberCMS.Core.Membership.Interfaces;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Membership.Parameters;
using ZauberCMS.Core.Shared.Models;
using ZauberCMS.Core.Shared.Services;
using ZauberCMS.Core.Tests.Infrastructure;
using AuditItem = ZauberCMS.Core.Audit.Models.Audit;
using ContentItemModel = ZauberCMS.Core.Content.Models.Content;

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

    private Task<EntryModel> GetEntryAsync(string slug, string? url) =>
        WithService<IContentService, EntryModel>(s => s.GetContentFromRequestAsync(
            new GetContentFromRequestParameters { Slug = slug, Url = url }, Ct));

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
