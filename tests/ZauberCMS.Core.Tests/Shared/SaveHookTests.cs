using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Content.Interfaces;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Content.Parameters;
using ZauberCMS.Core.Media.Interfaces;
using ZauberCMS.Core.Media.Models;
using ZauberCMS.Core.Media.Parameters;
using ZauberCMS.Core.Tags.Interfaces;
using ZauberCMS.Core.Tags.Models;
using ZauberCMS.Core.Tags.Parameters;
using ZauberCMS.Core.Tests.Infrastructure;
using ContentItem = ZauberCMS.Core.Content.Models.Content;
using MediaItem = ZauberCMS.Core.Media.Models.Media;

namespace ZauberCMS.Core.Tests.Shared;

/// <summary>
/// Roadmap D28 (Phase 10b): the save hooks run for every row a change writes, of every type, each with its own state,
/// and one veto stops the whole change. ZA-012 (sort, move, recycle, content types), ZA-157 (media below an item, an
/// emptied bin), ZA-159 (drafts), ZA-018 (tag links).
/// </summary>
public class SaveHookTests : IAsyncLifetime
{
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

    private async Task<T> With<T, TService>(Func<TService, Task<T>> call) where TService : notnull
    {
        using var scope = _site.CreateScope();
        return await call(scope.ServiceProvider.GetRequiredService<TService>());
    }

    private Task<ContentItem> StoredAsync(Guid id) =>
        _site.QueryDatabaseAsync(db => db.Contents.AsNoTracking().SingleAsync(x => x.Id == id, Ct));

    private Task<int> AuditCountAsync() => _site.QueryDatabaseAsync(db => db.Audits.CountAsync(Ct));

    [Fact]
    public async Task ZA012_A_move_runs_the_hooks_for_every_page_whose_path_it_rewrites()
    {
        var from = await _site.CreateContentAsync(_pageType, "From");
        var to = await _site.CreateContentAsync(_pageType, "To");
        var moved = await _site.CreateContentAsync(_pageType, "Moved", from.Id);
        var below = await _site.CreateContentAsync(_pageType, "Below", moved.Id);
        var copy = await StoredAsync(moved.Id);
        copy.ParentId = to.Id;
        copy.IsRootContent = false;
        using var recorder = SaveHookRecorder.Start();

        var result = await With<HandlerResultOf, IContentService>(async s => new HandlerResultOf(
            await s.SaveContentAsync(new SaveContentParameters { Content = copy, ExcludePropertyData = true }, Ct)));

        Assert.True(result.Success, result.Messages);
        var modified = recorder.Before<ContentItem>(EntityState.Modified).Select(x => x.Id).ToList();
        Assert.Contains(moved.Id, modified);
        Assert.Contains(below.Id, modified);
        Assert.Equal(modified, recorder.After<ContentItem>(EntityState.Modified).Select(x => x.Id).ToList());
    }

    [Fact]
    public async Task ZA012_A_veto_on_a_page_below_stops_the_whole_recycle()
    {
        var page = await _site.CreateContentAsync(_pageType, "Page");
        var child = await _site.CreateContentAsync(_pageType, "Child", page.Id);
        var auditsBefore = await AuditCountAsync();
        using var recorder = SaveHookRecorder.Start();
        recorder.Veto = (row, _) => row is ContentItem c && c.Id == child.Id;

        var vetoed = await With<HandlerResultOf, IContentService>(async s => new HandlerResultOf(
            await s.DeleteContentAsync(new DeleteContentParameters { ContentId = page.Id, MoveToRecycleBin = true },
                Ct)));

        Assert.False(vetoed.Success);
        Assert.False((await StoredAsync(page.Id)).Deleted);
        Assert.False((await StoredAsync(child.Id)).Deleted);
        Assert.Equal(auditsBefore, await AuditCountAsync());

        recorder.Veto = null;
        recorder.Clear();
        var binned = await With<HandlerResultOf, IContentService>(async s => new HandlerResultOf(
            await s.DeleteContentAsync(new DeleteContentParameters { ContentId = page.Id, MoveToRecycleBin = true },
                Ct)));

        Assert.True(binned.Success, binned.Messages);
        var modified = recorder.Before<ContentItem>(EntityState.Modified);
        Assert.Contains(modified, x => x.Id == page.Id && x.Deleted);
        Assert.Contains(modified, x => x.Id == child.Id && x.Deleted);
        Assert.Equal(auditsBefore + 1, await AuditCountAsync());
    }

    [Fact]
    public async Task ZA012_A_content_type_save_shows_the_hooks_the_new_type_and_a_veto_saves_nothing()
    {
        var auditsBefore = await AuditCountAsync();
        using var recorder = SaveHookRecorder.Start();
        recorder.Veto = (row, _) => row is ContentType;

        var vetoed = await With<HandlerResultOf, IContentService>(async s => new HandlerResultOf(
            await s.SaveContentTypeAsync(new SaveContentTypeParameters
            {
                ContentType = new ContentType { Name = "Article", Alias = "article" }
            }, Ct)));

        Assert.False(vetoed.Success);
        // Before the fix the audit save wrote the type first: the hook saw it Unchanged and could not stop it
        Assert.Single(recorder.Before<ContentType>(EntityState.Added));
        Assert.False(await _site.QueryDatabaseAsync(db => db.ContentTypes.AnyAsync(x => x.Alias == "article", Ct)));
        Assert.Equal(auditsBefore, await AuditCountAsync());
    }

    [Fact]
    public async Task ZA157_Binning_and_emptying_the_bin_run_the_hooks_for_the_media_below_and_a_veto_stops_both()
    {
        var folder = await SaveMediaAsync(new MediaItem { Name = "Folder", MediaType = MediaType.Folder });
        var inner = await SaveMediaAsync(new MediaItem { Name = "Inner", MediaType = MediaType.Folder, ParentId = folder.Id });
        using var recorder = SaveHookRecorder.Start();

        var binned = await With<HandlerResultOf, IMediaService>(async s => new HandlerResultOf(
            await s.DeleteMediaAsync(new DeleteMediaParameters { MediaId = folder.Id }, Ct)));

        Assert.True(binned.Success, binned.Messages);
        var modified = recorder.Before<MediaItem>(EntityState.Modified);
        Assert.Contains(modified, x => x.Id == folder.Id && x.Deleted);
        Assert.Contains(modified, x => x.Id == inner.Id && x.Deleted);

        recorder.Clear();
        recorder.Veto = (row, state) => row is MediaItem m && m.Id == inner.Id && state == EntityState.Deleted;
        var vetoed = await With<HandlerResultOf, IMediaService>(async s => new HandlerResultOf(
            await s.EmptyMediaRecycleBinAsync(new EmptyMediaRecycleBinParameters { DeleteFiles = false }, Ct)));

        Assert.False(vetoed.Success);
        Assert.Equal(2, await _site.QueryDatabaseAsync(db =>
            db.Medias.CountAsync(x => x.Id == folder.Id || x.Id == inner.Id, Ct)));

        recorder.Clear();
        recorder.Veto = null;
        var emptied = await With<HandlerResultOf, IMediaService>(async s => new HandlerResultOf(
            await s.EmptyMediaRecycleBinAsync(new EmptyMediaRecycleBinParameters { DeleteFiles = false }, Ct)));

        Assert.True(emptied.Success, emptied.Messages);
        Assert.Equal(new[] { folder.Id, inner.Id }.Order(),
            recorder.After<MediaItem>(EntityState.Deleted).Select(x => x.Id).Order());
        Assert.Equal(0, await _site.QueryDatabaseAsync(db =>
            db.Medias.CountAsync(x => x.Id == folder.Id || x.Id == inner.Id, Ct)));
    }

    [Fact]
    public async Task ZA159_A_draft_runs_the_draft_hooks_and_clearing_it_runs_them_for_its_removal()
    {
        var page = await _site.CreateContentAsync(_pageType, "Page");
        var copy = await StoredAsync(page.Id);
        copy.Name = "Page (draft)";
        using var recorder = SaveHookRecorder.Start();

        var drafted = await With<HandlerResultOf, IContentService>(async s => new HandlerResultOf(
            await s.SaveContentAsync(new SaveContentParameters { Content = copy, SaveUnpublishedOnly = true }, Ct)));

        Assert.True(drafted.Success, drafted.Messages);
        var draft = Assert.Single(recorder.Before<UnpublishedContent>(EntityState.Added));
        Assert.Equal("Page (draft)", draft.JsonContent.Name);
        // The live page is not published by a draft: only its link to the draft changes
        Assert.DoesNotContain(recorder.Before<ContentItem>(EntityState.Modified), x => x.Name == "Page (draft)");

        recorder.Clear();
        var cleared = await With<HandlerResultOf, IContentService>(async s => new HandlerResultOf(
            await s.ClearUnpublishedContentAsync(new ClearUnpublishedContentParameters { ContentId = page.Id }, Ct)));

        Assert.True(cleared.Success, cleared.Messages);
        Assert.Single(recorder.After<UnpublishedContent>(EntityState.Deleted));
        Assert.Contains(recorder.After<ContentItem>(EntityState.Modified), x => x.Id == page.Id);
    }

    [Fact]
    public async Task ZA018_Tag_links_go_through_the_hooks_and_a_veto_leaves_no_link_and_no_audit_row()
    {
        var page = await _site.CreateContentAsync(_pageType, "Page");
        var tag = await With<Tag, ITagService>(async s =>
            (await s.SaveTagAsync(new SaveTagParameters { TagName = "News" }, Ct)).Entity!);
        var auditsBefore = await AuditCountAsync();
        using var recorder = SaveHookRecorder.Start();
        recorder.Veto = (row, _) => row is TagItem;

        var vetoed = await With<HandlerResultOf, ITagService>(async s => new HandlerResultOf(
            await s.SaveTagItemAsync(new SaveTagItemParameters { ItemId = page.Id, TagIds = [tag.Id] }, Ct)));

        Assert.False(vetoed.Success);
        Assert.Single(recorder.Before<TagItem>(EntityState.Added));
        Assert.False(await _site.QueryDatabaseAsync(db => db.TagItems.AnyAsync(x => x.ItemId == page.Id, Ct)));
        Assert.Equal(auditsBefore, await AuditCountAsync());

        recorder.Veto = null;
        var linked = await With<HandlerResultOf, ITagService>(async s => new HandlerResultOf(
            await s.SaveTagItemAsync(new SaveTagItemParameters { ItemId = page.Id, TagIds = [tag.Id] }, Ct)));

        Assert.True(linked.Success, linked.Messages);
        Assert.True(await _site.QueryDatabaseAsync(db => db.TagItems.AnyAsync(x => x.ItemId == page.Id, Ct)));
        Assert.Equal(auditsBefore + 1, await AuditCountAsync());
    }

    [Fact]
    public async Task A_save_from_the_callers_copy_runs_the_hooks_once_for_the_row_that_is_saved()
    {
        var folder = await SaveMediaAsync(new MediaItem { Name = "Folder", MediaType = MediaType.Folder });
        var copy = await _site.QueryDatabaseAsync(db => db.Medias.AsNoTracking().SingleAsync(x => x.Id == folder.Id, Ct));
        copy.Name = "Renamed";
        using var recorder = SaveHookRecorder.Start();

        await SaveMediaAsync(copy, isUpdate: true);

        var before = recorder.Before<MediaItem>(EntityState.Modified);
        var row = Assert.Single(before, x => x.Id == folder.Id);
        Assert.NotSame(copy, row);
        Assert.Empty(recorder.Before<MediaItem>(EntityState.Detached));
    }

    private async Task<MediaItem> SaveMediaAsync(MediaItem media, bool isUpdate = false)
    {
        var result = await With<ZauberCMS.Core.Shared.Models.HandlerResult<MediaItem>, IMediaService>(s =>
            s.SaveMediaAsync(new SaveMediaParameters { MediaToSave = media, IsUpdate = isUpdate }, Ct));
        Assert.True(result.Success, TestSite.Describe(result.Messages));
        return result.Entity!;
    }

    /// <summary>Success and messages of any service result, so one helper serves every service.</summary>
    private sealed record HandlerResultOf(bool Success, string Messages)
    {
        public HandlerResultOf(ZauberCMS.Core.Shared.Models.HandlerResult<ContentItem> r)
            : this(r.Success, TestSite.Describe(r.Messages)) { }

        public HandlerResultOf(ZauberCMS.Core.Shared.Models.HandlerResult<ContentType> r)
            : this(r.Success, TestSite.Describe(r.Messages)) { }

        public HandlerResultOf(ZauberCMS.Core.Shared.Models.HandlerResult<MediaItem> r)
            : this(r.Success, TestSite.Describe(r.Messages)) { }

        public HandlerResultOf(ZauberCMS.Core.Shared.Models.HandlerResult<int?> r)
            : this(r.Success, TestSite.Describe(r.Messages)) { }

        public HandlerResultOf(ZauberCMS.Core.Shared.Models.HandlerResult<UnpublishedContent> r)
            : this(r.Success, TestSite.Describe(r.Messages)) { }

        public HandlerResultOf(ZauberCMS.Core.Shared.Models.HandlerResult<TagItem> r)
            : this(r.Success, TestSite.Describe(r.Messages)) { }
    }
}

/// <summary>The same tests on a real PostgreSQL (Roadmap D15); skipped without Docker.</summary>
public class SaveHookTestsOnPostgreSql : SaveHookTests
{
    protected override string DatabaseProvider => TestSite.PostgreSqlProvider;
}

/// <summary>The same tests on SQL Server; skipped without <c>ZAUBER_TEST_SQLSERVER</c>.</summary>
public class SaveHookTestsOnSqlServer : SaveHookTests
{
    protected override string DatabaseProvider => TestSite.SqlServerProvider;
}
