using Microsoft.AspNetCore.Components.Forms;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Media.Interfaces;
using ZauberCMS.Core.Media.Models;
using ZauberCMS.Core.Media.Parameters;
using ZauberCMS.Core.Plugins;
using ZauberCMS.Core.Providers;
using ZauberCMS.Core.Shared.Interfaces;
using ZauberCMS.Core.Shared.Models;
using ZauberCMS.Core.Shared.Services;
using ZauberCMS.Core.Tests.Infrastructure;
using MediaItem = ZauberCMS.Core.Media.Models.Media;

namespace ZauberCMS.Core.Tests.Shared;

/// <summary>
/// ZA-163 (Roadmap D62): <see cref="IAfterEntityCommit{TEntity}"/> runs once the change is committed: at once for a
/// save without a transaction, at the commit of the caller's transaction, never for a change rolled back after the
/// after-save hooks ran (a media purge whose file can't be removed). <see cref="IAfterEntitySave{TEntity}"/> keeps
/// running inside the transaction, before the commit.
/// </summary>
public class AfterCommitHookTests : IAsyncLifetime
{
    protected virtual string DatabaseProvider => TestSite.SqliteProvider;

    private TestSite _site = null!;

    public async ValueTask InitializeAsync() => _site = await TestSite.CreateAsync(
        new Dictionary<string, string?>
        {
            // Only a delete of a file under "locked/" fails; every other call is the disk provider's
            ["Zauber:Plugins:StorageProvider"] = typeof(LockedFileStorageProvider).FullName
        },
        databaseProvider: DatabaseProvider);

    public async ValueTask DisposeAsync() => await _site.DisposeAsync();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ZA163_A_save_without_a_transaction_runs_the_after_commit_hooks_once_it_is_stored()
    {
        using var recorder = SaveHookRecorder.Start();
        var media = await AddMediaAsync("photo");

        var committed = Assert.Single(recorder.Committed<MediaItem>(EntityState.Added));
        Assert.Equal(media.Id, committed.Id);
        Assert.Single(recorder.After<MediaItem>(EntityState.Added));
    }

    [Fact]
    public async Task ZA163_Inside_a_transaction_the_after_commit_hooks_wait_for_the_commit()
    {
        using var scope = _site.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        using var recorder = SaveHookRecorder.Start();

        await using var transaction = await db.Database.BeginTransactionAsync(Ct);
        var media = NewMedia("first");
        db.Medias.Add(media);
        Assert.True((await SaveAsync(scope, db, media)).Success);
        var second = NewMedia("second");
        db.Medias.Add(second);
        Assert.True((await SaveAsync(scope, db, second)).Success);

        // After-save ran inside the transaction; after-commit not yet
        Assert.Equal(2, recorder.After<MediaItem>(EntityState.Added).Count);
        Assert.Equal(0, recorder.CommittedCount);

        await transaction.CommitAsync(Ct);

        Assert.Equal([media.Id, second.Id], recorder.Committed<MediaItem>(EntityState.Added).Select(x => x.Id));
    }

    [Fact]
    public async Task ZA163_A_rolled_back_transaction_never_reaches_the_after_commit_hooks()
    {
        using var scope = _site.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        using var recorder = SaveHookRecorder.Start();

        await using (var transaction = await db.Database.BeginTransactionAsync(Ct))
        {
            var media = NewMedia("rolled back");
            db.Medias.Add(media);
            Assert.True((await SaveAsync(scope, db, media)).Success);
            await transaction.RollbackAsync(Ct);
        }

        // One disposed without a commit or a rollback is not committed either
        await using (await db.Database.BeginTransactionAsync(Ct))
        {
            var media = NewMedia("disposed");
            db.Medias.Add(media);
            Assert.True((await SaveAsync(scope, db, media)).Success);
        }

        Assert.Equal(2, recorder.After<MediaItem>(EntityState.Added).Count);
        Assert.Equal(0, recorder.CommittedCount);

        // A later save on the same context reports only its own row
        ((DbContext)db).ChangeTracker.Clear();
        var stored = NewMedia("stored");
        db.Medias.Add(stored);
        Assert.True((await SaveAsync(scope, db, stored)).Success);
        Assert.Equal(stored.Id, Assert.Single(recorder.Committed<MediaItem>(EntityState.Added)).Id);
    }

    [Fact]
    public async Task ZA163_A_synchronous_commit_runs_the_after_commit_hooks_too()
    {
        using var scope = _site.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        using var recorder = SaveHookRecorder.Start();

        using var transaction = db.Database.BeginTransaction();
        var media = NewMedia("sync");
        db.Medias.Add(media);
        Assert.True((await SaveAsync(scope, db, media)).Success);
        Assert.Equal(0, recorder.CommittedCount);

        // The sync-over-async path for a site's code that commits synchronously
#pragma warning disable CA1849
        transaction.Commit();
#pragma warning restore CA1849

        Assert.Equal(media.Id, Assert.Single(recorder.Committed<MediaItem>(EntityState.Added)).Id);
    }

    [Fact]
    public async Task ZA163_A_media_purge_rolled_back_because_a_file_cannot_be_removed_is_not_reported_as_done()
    {
        var media = await AddMediaAsync("locked", url: $"{LockedFileStorageProvider.LockedFolder}/{Guid.NewGuid()}/locked.png");
        Assert.True((await DeleteAsync(media.Id)).Success);
        using var recorder = SaveHookRecorder.Start();

        var result = await DeleteAsync(media.Id, permanently: true);

        Assert.False(result.Success);
        Assert.NotNull(await StoredAsync(media.Id));
        // The after-save hooks saw the delete inside the transaction; it was rolled back, so after-commit saw nothing
        Assert.Equal(media.Id, Assert.Single(recorder.After<MediaItem>(EntityState.Deleted)).Id);
        Assert.Equal(0, recorder.CommittedCount);
    }

    [Fact]
    public async Task ZA163_A_media_purge_that_commits_reports_every_deleted_row_after_the_commit()
    {
        var folder = await AddMediaAsync("folder", MediaType.Folder);
        var file = await AddMediaAsync("file", parentId: folder.Id);
        Assert.True((await DeleteAsync(folder.Id)).Success);
        using var recorder = SaveHookRecorder.Start();

        var result = await DeleteAsync(folder.Id, permanently: true);

        Assert.True(result.Success, TestSite.Describe(result.Messages));
        Assert.Null(await StoredAsync(folder.Id));
        Assert.Equal(new[] { folder.Id, file.Id }.Order(),
            recorder.Committed<MediaItem>(EntityState.Deleted).Select(x => x.Id).Order());
    }

    [Fact]
    public async Task ZA163_An_after_commit_hook_that_throws_leaves_the_change_stored_and_the_other_hooks_run()
    {
        using var recorder = SaveHookRecorder.Start();
        recorder.ThrowOnCommit = true;

        var media = await AddMediaAsync("thrown");

        Assert.NotNull(await StoredAsync(media.Id));
        // ThrowingMediaCommitHook runs first (SortOrder -1) and throws; the recording hook still ran
        Assert.Equal(media.Id, Assert.Single(recorder.Committed<MediaItem>(EntityState.Added)).Id);
    }

    [Fact]
    public void ZA163_The_after_commit_hook_is_a_discovery_interface()
    {
        Assert.Contains(typeof(IAfterEntityCommit<>).FullName!, ExtensionManager.DiscoveryInterfaces);
        using var scope = _site.CreateScope();
        var hooks = scope.ServiceProvider.GetRequiredService<ExtensionManager>()
            .GetInstances<IAfterEntityCommit<MediaItem>>();
        Assert.Contains(hooks.Values, h => h is RecordingMediaSaveHook);
    }

    private static MediaItem NewMedia(string name) => new()
    {
        Name = name, MediaType = MediaType.Image, Url = $"media/{Guid.NewGuid()}/{name}.png"
    };

    private static Task<HandlerResult<MediaItem>> SaveAsync(IServiceScope scope, IZauberDbContext db, MediaItem media) =>
        db.SaveChangesAndLog(media, new HandlerResult<MediaItem>(),
            scope.ServiceProvider.GetRequiredService<ICacheService>(),
            scope.ServiceProvider.GetRequiredService<ExtensionManager>(), Ct);

    private async Task<MediaItem> AddMediaAsync(string name, MediaType type = MediaType.Image, Guid? parentId = null,
        string? url = null)
    {
        using var scope = _site.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<IMediaService>().SaveMediaAsync(
            new SaveMediaParameters
            {
                MediaToSave = new MediaItem
                {
                    Name = name, MediaType = type, ParentId = parentId,
                    Url = type == MediaType.Folder ? null : url ?? $"media/{Guid.NewGuid()}/{name}.png"
                }
            }, Ct);
        Assert.True(result.Success, TestSite.Describe(result.Messages));
        return result.Entity!;
    }

    private async Task<HandlerResult<MediaItem>> DeleteAsync(Guid id, bool permanently = false)
    {
        using var scope = _site.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IMediaService>().DeleteMediaAsync(
            new DeleteMediaParameters { MediaId = id, MoveToRecycleBin = !permanently }, Ct);
    }

    private Task<MediaItem?> StoredAsync(Guid id) =>
        _site.QueryDatabaseAsync(db => db.Medias.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, Ct));
}

/// <summary>The same tests on a real PostgreSQL (Roadmap D15); skipped without Docker.</summary>
public class AfterCommitHookTestsOnPostgreSql : AfterCommitHookTests
{
    protected override string DatabaseProvider => TestSite.PostgreSqlProvider;
}

/// <summary>The same tests on SQL Server; skipped without <c>ZAUBER_TEST_SQLSERVER</c>.</summary>
public class AfterCommitHookTestsOnSqlServer : AfterCommitHookTests
{
    protected override string DatabaseProvider => TestSite.SqlServerProvider;
}

/// <summary>
/// A storage provider whose delete fails for files under <see cref="LockedFolder"/>, as a file held open by another
/// process does on Windows (a test can't lock a file that way on Linux). Chosen only by the tests that configure it.
/// </summary>
public class LockedFileStorageProvider(IServiceProvider services) : IStorageProvider
{
    public const string LockedFolder = "locked";

    private readonly DiskStorageProvider disk = ActivatorUtilities.CreateInstance<DiskStorageProvider>(services);

    public Task<HandlerResult<MediaItem>> SaveFile(IBrowserFile browserFile, MediaItem? existingMedia = null,
        string? parentFolderName = null, bool overwrite = true) =>
        disk.SaveFile(browserFile, existingMedia, parentFolderName, overwrite);

    public Task<bool> DeleteFile(string? url) =>
        url?.StartsWith(LockedFolder + "/", StringComparison.Ordinal) == true
            ? throw new IOException("The file is in use by another process")
            : disk.DeleteFile(url);

    public Task<HandlerResult<MediaItem>> CanUseFile(IBrowserFile browserFile, bool onlyImages = false) =>
        disk.CanUseFile(browserFile, onlyImages);
}
