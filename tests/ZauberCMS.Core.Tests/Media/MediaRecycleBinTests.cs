using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Media.Interfaces;
using ZauberCMS.Core.Media.Models;
using ZauberCMS.Core.Media.Parameters;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Shared;
using ZauberCMS.Core.Shared.Models;
using ZauberCMS.Core.Tests.Infrastructure;
using MediaItem = ZauberCMS.Core.Media.Models.Media;

namespace ZauberCMS.Core.Tests.Media;

/// <summary>
/// The media recycle bin (Roadmap D3, ZA-006): deleting moves an item and everything below it to the bin, Restore
/// brings that group back, a permanent delete removes the rows and the files. ZA-013: the media events are raised
/// once, by the service, after the save.
/// </summary>
public class MediaRecycleBinTests : IAsyncLifetime
{
    /// <summary>Overridden by the PostgreSQL copy of this class at the end of the file.</summary>
    protected virtual string DatabaseProvider => TestSite.SqliteProvider;

    private TestSite _site = null!;

    public async ValueTask InitializeAsync() => _site = await TestSite.CreateAsync(databaseProvider: DatabaseProvider);

    public async ValueTask DisposeAsync() => await _site.DisposeAsync();

    [Fact]
    public async Task Deleting_a_folder_moves_it_and_everything_in_it_to_the_recycle_bin()
    {
        var folder = await AddMediaAsync("Folder", MediaType.Folder);
        var sub = await AddMediaAsync("Sub", MediaType.Folder, folder.Id);
        var file = await AddMediaAsync("File", MediaType.Image, sub.Id);

        var result = await DeleteAsync(folder.Id);

        Assert.True(result.Success, TestSite.Describe(result.Messages));
        var stored = await StoredAsync(folder.Id, sub.Id, file.Id);
        Assert.All(stored, x => Assert.True(x.Deleted));
        Assert.Single(stored.Select(x => x.DateUpdated).Distinct());
    }

    [Fact]
    public async Task Restore_brings_back_the_items_that_went_to_the_bin_with_it_but_not_one_binned_earlier()
    {
        var folder = await AddMediaAsync("Folder", MediaType.Folder);
        var earlier = await AddMediaAsync("Binned earlier", MediaType.Image, folder.Id);
        var withIt = await AddMediaAsync("Binned with the folder", MediaType.Image, folder.Id);
        Assert.True((await DeleteAsync(earlier.Id)).Success);
        await Task.Delay(20, TestContext.Current.CancellationToken);
        Assert.True((await DeleteAsync(folder.Id)).Success);

        var result = await WithServiceAsync(s => s.RestoreMediaAsync(new RestoreMediaParameters { MediaId = folder.Id }));

        Assert.True(result.Success, TestSite.Describe(result.Messages));
        var stored = (await StoredAsync(folder.Id, earlier.Id, withIt.Id)).ToDictionary(x => x.Id);
        Assert.False(stored[folder.Id].Deleted);
        Assert.False(stored[withIt.Id].Deleted);
        Assert.True(stored[earlier.Id].Deleted);
    }

    [Fact]
    public async Task Restore_is_refused_while_the_parent_folder_is_in_the_recycle_bin()
    {
        var folder = await AddMediaAsync("Folder", MediaType.Folder);
        var file = await AddMediaAsync("File", MediaType.Image, folder.Id);
        Assert.True((await DeleteAsync(folder.Id)).Success);

        var result = await WithServiceAsync(s => s.RestoreMediaAsync(new RestoreMediaParameters { MediaId = file.Id }));

        Assert.False(result.Success);
        Assert.True((await StoredAsync(file.Id)).Single().Deleted);
    }

    [Fact]
    public async Task Saving_a_form_loaded_before_the_delete_is_refused_and_the_group_still_restores()
    {
        var folder = await AddMediaAsync("Folder", MediaType.Folder);
        var file = await AddMediaAsync("File", MediaType.Image, folder.Id);
        var loaded = await WithServiceAsync(s => s.GetMediaAsync(new GetMediaParameters { Id = folder.Id }));
        Assert.True((await DeleteAsync(folder.Id)).Success);

        loaded!.Name = "Renamed";
        var result = await WithServiceAsync(s => s.SaveMediaAsync(new SaveMediaParameters { MediaToSave = loaded, IsUpdate = true }));

        Assert.False(result.Success);
        var stored = (await StoredAsync(folder.Id)).Single();
        Assert.True(stored.Deleted);
        Assert.Equal("Folder", stored.Name);

        // Its recycle stamp is untouched, so Restore still finds what went to the bin with it
        Assert.True((await WithServiceAsync(s => s.RestoreMediaAsync(new RestoreMediaParameters { MediaId = folder.Id }))).Success);
        Assert.All(await StoredAsync(folder.Id, file.Id), x => Assert.False(x.Deleted));
    }

    [Fact]
    public async Task A_folder_never_lists_its_binned_children()
    {
        var folder = await AddMediaAsync("Folder", MediaType.Folder);
        var binned = await AddMediaAsync("Binned", MediaType.Image, folder.Id);
        var live = await AddMediaAsync("Live", MediaType.Image, folder.Id);
        Assert.True((await DeleteAsync(binned.Id)).Success);

        var loaded = await WithServiceAsync(s => s.GetMediaAsync(new GetMediaParameters { Id = folder.Id, IncludeChildren = true }));

        Assert.Equal([live.Id], loaded!.Children.Select(x => x.Id));
    }

    [Fact]
    public async Task Nothing_can_be_saved_into_a_folder_that_is_in_the_recycle_bin()
    {
        var folder = await AddMediaAsync("Folder", MediaType.Folder);
        Assert.True((await DeleteAsync(folder.Id)).Success);

        var result = await WithServiceAsync(s => s.SaveMediaAsync(new SaveMediaParameters
        {
            MediaToSave = new MediaItem { Name = "Late upload", MediaType = MediaType.Image, Url = "media/x/late.png" },
            ParentFolderId = folder.Id
        }));

        Assert.False(result.Success);
        Assert.Empty(await _site.QueryDatabaseAsync(db =>
            db.Medias.AsNoTracking().Where(x => x.Name == "Late upload").ToListAsync()));
    }

    [Fact]
    public async Task Usage_of_the_whole_bin_looks_at_every_binned_item()
    {
        var file = await AddMediaAsync("Binned", MediaType.Image);
        Assert.True((await DeleteAsync(file.Id)).Success);
        var pageType = await _site.CreateContentTypeAsync("page");
        var page = await _site.CreateContentAsync(pageType, "Picks it");
        await _site.QueryDatabaseAsync(async db =>
        {
            db.ContentPropertyValues.Add(PropertyValue(page.Id, $"[\"{file.Id}\"]"));
            return await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        var usages = await WithServiceAsync(s => s.GetMediaUsageAsync(new GetMediaUsageParameters
        {
            AllInRecycleBin = true,
            IncludeDescendants = false
        }));

        Assert.Equal(page.Id, Assert.Single(usages).ItemId);
    }

    [Fact]
    public async Task A_permanent_delete_removes_the_rows_the_files_and_their_upload_folders()
    {
        var folder = await AddMediaAsync("Folder", MediaType.Folder);
        var file = await AddMediaWithFileAsync("photo.png", folder.Id);
        Assert.True((await DeleteAsync(folder.Id)).Success);
        var path = PhysicalPath(file.Url!);
        Assert.True(File.Exists(path));

        var result = await DeleteAsync(folder.Id, permanently: true);

        Assert.True(result.Success, TestSite.Describe(result.Messages));
        Assert.Empty(await StoredAsync(folder.Id, file.Id));
        Assert.False(File.Exists(path));
        Assert.False(Directory.Exists(Path.GetDirectoryName(path)));
        Assert.True(Directory.Exists(Path.Combine(_site.App.Environment.WebRootPath, "media")));
    }

    [Fact]
    public async Task A_permanent_delete_keeps_a_file_that_another_media_item_still_uses()
    {
        var binned = await AddMediaWithFileAsync("shared.png");
        await AddMediaAsync("Same file", MediaType.Image, url: binned.Url);
        Assert.True((await DeleteAsync(binned.Id)).Success);

        var result = await DeleteAsync(binned.Id, permanently: true);

        Assert.True(result.Success, TestSite.Describe(result.Messages));
        Assert.True(File.Exists(PhysicalPath(binned.Url!)));
    }

    [Fact]
    public async Task A_permanent_delete_is_refused_when_live_media_is_below()
    {
        var folder = await AddMediaAsync("Folder", MediaType.Folder);
        var file = await AddMediaAsync("File", MediaType.Image, folder.Id);

        var result = await DeleteAsync(folder.Id, permanently: true);

        Assert.False(result.Success);
        Assert.Equal(2, (await StoredAsync(folder.Id, file.Id)).Count);
    }

    [Fact]
    public async Task Emptying_the_bin_deletes_every_binned_item_and_nothing_live()
    {
        var folder = await AddMediaAsync("Folder", MediaType.Folder);
        var inFolder = await AddMediaWithFileAsync("in-folder.png", folder.Id);
        var single = await AddMediaAsync("Single", MediaType.Image);
        var live = await AddMediaAsync("Live", MediaType.Image);
        Assert.True((await DeleteAsync(folder.Id)).Success);
        Assert.True((await DeleteAsync(single.Id)).Success);

        var result = await WithServiceAsync(s => s.EmptyMediaRecycleBinAsync(new EmptyMediaRecycleBinParameters()));

        Assert.True(result.Success, TestSite.Describe(result.Messages));
        Assert.Equal(3, result.Entity);
        Assert.Equal([live.Id], (await StoredAsync(folder.Id, inFolder.Id, single.Id, live.Id)).Select(x => x.Id));
        Assert.False(File.Exists(PhysicalPath(inFolder.Url!)));
    }

    [Fact]
    public async Task Emptying_the_bin_keeps_a_binned_folder_that_still_holds_live_media()
    {
        var folder = await AddMediaAsync("Folder", MediaType.Folder);
        Assert.True((await DeleteAsync(folder.Id)).Success);
        // Not possible from the admin, but the data allows it: the folder row must stay or the live item loses its parent
        var live = await AddMediaAsync("Live inside", MediaType.Image, folder.Id);

        var result = await WithServiceAsync(s => s.EmptyMediaRecycleBinAsync(new EmptyMediaRecycleBinParameters()));

        Assert.True(result.Success, TestSite.Describe(result.Messages));
        Assert.Equal(2, (await StoredAsync(folder.Id, live.Id)).Count);
    }

    [Fact]
    public async Task Deleting_raises_the_media_deleted_event_once_and_after_the_save()
    {
        var file = await AddMediaAsync("File", MediaType.Image);
        var appState = _site.Services.GetRequiredService<AppState>();
        var raised = new List<bool>();
        Func<MediaItem?, string, Task> handler = async (media, _) =>
            raised.Add((await StoredAsync(file.Id)).Single().Deleted);
        appState.OnMediaDeleted += handler;
        try
        {
            Assert.True((await DeleteAsync(file.Id)).Success);
        }
        finally
        {
            appState.OnMediaDeleted -= handler;
        }

        Assert.Equal([true], raised);
    }

    // ZA-013: the permanent delete raised its event before the save
    [Fact]
    public async Task A_permanent_delete_raises_the_media_deleted_event_once_and_after_the_save()
    {
        var file = await AddMediaAsync("File", MediaType.Image);
        Assert.True((await DeleteAsync(file.Id)).Success);
        var appState = _site.Services.GetRequiredService<AppState>();
        var raised = new List<int>();
        Func<MediaItem?, string, Task> handler = async (_, _) => raised.Add((await StoredAsync(file.Id)).Count);
        appState.OnMediaDeleted += handler;
        try
        {
            Assert.True((await DeleteAsync(file.Id, permanently: true)).Success);
        }
        finally
        {
            appState.OnMediaDeleted -= handler;
        }

        Assert.Equal([0], raised);
    }

    [Fact]
    public async Task Usage_finds_a_link_to_a_file_whose_url_does_not_contain_its_id()
    {
        // A storage provider other than the disk one: the URL is all a rich text link has
        var file = await AddMediaAsync("cdn.png", MediaType.Image, url: "https://cdn.example.test/files/photo-2026.png");
        var pageType = await _site.CreateContentTypeAsync("page");
        var page = await _site.CreateContentAsync(pageType, "Links to it");
        await _site.QueryDatabaseAsync(async db =>
        {
            db.ContentPropertyValues.Add(PropertyValue(page.Id, "<img src=\"https://cdn.example.test/files/photo-2026.png\">"));
            return await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        var usages = await WithServiceAsync(s => s.GetMediaUsageAsync(new GetMediaUsageParameters { MediaIds = [file.Id] }));

        Assert.Equal(page.Id, Assert.Single(usages).ItemId);
    }

    [Fact]
    public async Task Usage_finds_pages_that_pick_the_media_and_rich_text_that_links_to_its_file()
    {
        var file = await AddMediaWithFileAsync("used.png");
        var pageType = await _site.CreateContentTypeAsync("page");
        var picker = await _site.CreateContentAsync(pageType, "Picks it");
        var richText = await _site.CreateContentAsync(pageType, "Links to it");
        var other = await _site.CreateContentAsync(pageType, "Does not");
        await _site.QueryDatabaseAsync(async db =>
        {
            db.ContentPropertyValues.Add(PropertyValue(picker.Id, $"[\"{file.Id}\"]"));
            db.ContentPropertyValues.Add(PropertyValue(richText.Id, $"<p><img src=\"/{file.Url}\"></p>"));
            db.ContentPropertyValues.Add(PropertyValue(other.Id, "<p>Nothing</p>"));
            db.UserPropertyValues.Add(new UserPropertyValue { UserId = _site.CurrentUser.Id, Alias = "photo", Value = $"[\"{file.Id}\"]" });
            return await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        var usages = await WithServiceAsync(s => s.GetMediaUsageAsync(new GetMediaUsageParameters { MediaIds = [file.Id] }));

        Assert.Equal(
            new[] { picker.Id, richText.Id }.Order(),
            usages.Where(u => u.Source == MediaUsageSource.Content).Select(u => u.ItemId).Order());
        Assert.Contains(usages, u => u.Source == MediaUsageSource.User && u.ItemId == _site.CurrentUser.Id);
    }

    [Fact]
    public async Task Usage_of_a_folder_includes_the_media_inside_it()
    {
        var folder = await AddMediaAsync("Folder", MediaType.Folder);
        var file = await AddMediaAsync("File", MediaType.Image, folder.Id);
        var pageType = await _site.CreateContentTypeAsync("page");
        var page = await _site.CreateContentAsync(pageType, "Picks the file");
        await _site.QueryDatabaseAsync(async db =>
        {
            db.ContentPropertyValues.Add(PropertyValue(page.Id, $"[\"{file.Id}\"]"));
            return await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        var usages = await WithServiceAsync(s => s.GetMediaUsageAsync(new GetMediaUsageParameters { MediaIds = [folder.Id] }));

        var usage = Assert.Single(usages);
        Assert.Equal(file.Id, usage.MediaId);
        Assert.Equal(page.Id, usage.ItemId);
    }

    private static ContentPropertyValue PropertyValue(Guid contentId, string value) =>
        new() { ContentId = contentId, Alias = "body", ContentTypePropertyId = Guid.NewGuid(), Value = value };

    private async Task<MediaItem> AddMediaAsync(string name, MediaType type, Guid? parentId = null, string? url = null)
    {
        var media = new MediaItem
        {
            Name = name,
            MediaType = type,
            ParentId = parentId,
            Url = url ?? (type == MediaType.Folder ? null : $"media/{Guid.NewGuid()}/{name}.png")
        };
        await _site.QueryDatabaseAsync(async db =>
        {
            db.Medias.Add(media);
            return await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        return media;
    }

    /// <summary>A media item with a real file where the disk storage provider puts it: media/{id}/{name}.</summary>
    private async Task<MediaItem> AddMediaWithFileAsync(string fileName, Guid? parentId = null)
    {
        var id = Guid.NewGuid();
        var url = $"media/{id}/{fileName}";
        var path = PhysicalPath(url);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, [1, 2, 3], TestContext.Current.CancellationToken);
        var media = new MediaItem { Id = id, Name = fileName, MediaType = MediaType.Image, ParentId = parentId, Url = url };
        await _site.QueryDatabaseAsync(async db =>
        {
            db.Medias.Add(media);
            return await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        return media;
    }

    private string PhysicalPath(string url) =>
        Path.Combine(_site.App.Environment.WebRootPath, url.Replace('/', Path.DirectorySeparatorChar));

    private Task<HandlerResult<MediaItem>> DeleteAsync(Guid id, bool permanently = false) =>
        WithServiceAsync(s => s.DeleteMediaAsync(new DeleteMediaParameters { MediaId = id, MoveToRecycleBin = !permanently }));

    private async Task<T> WithServiceAsync<T>(Func<IMediaService, Task<T>> call)
    {
        using var scope = _site.CreateScope();
        return await call(scope.ServiceProvider.GetRequiredService<IMediaService>());
    }

    private Task<List<MediaItem>> StoredAsync(params Guid[] ids) =>
        _site.QueryDatabaseAsync(db => db.Medias.AsNoTracking().Where(x => ids.Contains(x.Id)).ToListAsync());
}

/// <summary>The same tests on a real PostgreSQL (Roadmap D15); skipped without Docker.</summary>
public class MediaRecycleBinTestsOnPostgreSql : MediaRecycleBinTests
{
    protected override string DatabaseProvider => TestSite.PostgreSqlProvider;
}

/// <summary>The same tests on SQL Server; skipped without <c>ZAUBER_TEST_SQLSERVER</c>.</summary>
public class MediaRecycleBinTestsOnSqlServer : MediaRecycleBinTests
{
    protected override string DatabaseProvider => TestSite.SqlServerProvider;
}
