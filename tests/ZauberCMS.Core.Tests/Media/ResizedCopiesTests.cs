using ImageResize.Core.Cache;
using ImageResize.Core.Configuration;
using ImageResize.Core.Interfaces;
using ImageResize.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SkiaSharp;
using ZauberCMS.Core.Media;
using ZauberCMS.Core.Media.Interfaces;
using ZauberCMS.Core.Media.Models;
using ZauberCMS.Core.Media.Parameters;
using ZauberCMS.Core.Providers;
using ZauberCMS.Core.Tests.Infrastructure;
using MediaItem = ZauberCMS.Core.Media.Models.Media;

namespace ZauberCMS.Core.Tests.Media;

/// <summary>
/// ZA-155 (Roadmap D63): the resized copies ImageResize makes of an image are kept in a folder per image, and a
/// permanent delete of the image (through the disk storage provider) removes them; other images keep theirs.
/// </summary>
public class ResizedCopiesTests : IAsyncLifetime
{
    private TestSite _site = null!;

    public async ValueTask InitializeAsync() => _site = await TestSite.CreateAsync();

    public async ValueTask DisposeAsync() => await _site.DisposeAsync();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private ImageResizeOptions ResizeOptions => _site.Services.GetRequiredService<IOptions<ImageResizeOptions>>().Value;

    [Fact]
    public async Task ZA155_Each_images_copies_are_kept_in_a_folder_of_their_own()
    {
        var photo = await AddImageAsync("photo.png");

        var small = await ResizeAsync(photo.Url!, 4);
        var smaller = await ResizeAsync(photo.Url!, 2);

        var folder = ResizedImageCache.FolderFor(ResizeOptions, photo.Url!);
        Assert.NotEqual(small, smaller);
        Assert.Equal(folder, Path.GetDirectoryName(small));
        Assert.Equal(folder, Path.GetDirectoryName(smaller));
        Assert.StartsWith(Path.GetFullPath(ResizeOptions.CacheRoot) + Path.DirectorySeparatorChar, folder);
        // The same image asked for with another case or a leading slash is the same image
        Assert.Equal(folder, ResizedImageCache.FolderFor(ResizeOptions, "/" + photo.Url!.ToUpperInvariant()));
        // The registered IImageCache stays ImageResize's own, so its start-up prune still runs
        Assert.IsType<FileSystemImageCache>(_site.Services.GetRequiredService<IImageCache>());
    }

    [Fact]
    public async Task ZA155_A_permanent_delete_removes_the_images_resized_copies_and_no_others()
    {
        var photo = await AddImageAsync("photo.png");
        var other = await AddImageAsync("other.png");
        var copies = new[] { await ResizeAsync(photo.Url!, 4), await ResizeAsync(photo.Url!, 2) };
        var otherCopy = await ResizeAsync(other.Url!, 4);
        Assert.All(copies, c => Assert.True(File.Exists(c)));

        Assert.True((await DeleteAsync(photo.Id)).Success);
        // In the bin the copies stay (a restore brings the image back)
        Assert.All(copies, c => Assert.True(File.Exists(c)));

        var purged = await DeleteAsync(photo.Id, permanently: true);

        Assert.True(purged.Success, TestSite.Describe(purged.Messages));
        Assert.All(copies, c => Assert.False(File.Exists(c)));
        Assert.False(Directory.Exists(ResizedImageCache.FolderFor(ResizeOptions, photo.Url!)));
        Assert.True(File.Exists(otherCopy));
    }

    [Fact]
    public async Task ZA155_Writing_a_file_over_one_of_the_same_name_removes_the_old_images_copies()
    {
        var photo = await AddImageAsync("photo.png");
        var copy = await ResizeAsync(photo.Url!, 4);

        using var scope = _site.CreateScope();
        var disk = ActivatorUtilities.CreateInstance<DiskStorageProvider>(scope.ServiceProvider);
        var saved = await disk.SaveFile(new PngBrowserFile("photo.png", Png(12)), photo, photo.Id.ToString());

        Assert.True(saved.Success, TestSite.Describe(saved.Messages));
        Assert.Equal(photo.Url, saved.Entity!.Url);
        Assert.False(File.Exists(copy));
    }

    [Fact]
    public void ZA155_Removing_the_copies_of_an_image_that_has_none_does_nothing()
    {
        ResizedImageCache.RemoveCopies(ResizeOptions, $"media/{Guid.NewGuid()}/none.png", NullLogger.Instance);
    }

    private async Task<string> ResizeAsync(string url, int width)
    {
        var resizer = _site.Services.GetRequiredService<IImageResizerService>();
        var result = await resizer.EnsureResizedAsync(url, new ResizeOptions(width, null, null), Ct);
        Assert.True(File.Exists(result.CachedPath));
        return result.CachedPath;
    }

    /// <summary>A PNG where the disk storage provider puts uploads (media/{id}/{name}) and its media row.</summary>
    private async Task<MediaItem> AddImageAsync(string fileName)
    {
        var id = Guid.NewGuid();
        var url = $"media/{id}/{fileName}";
        var path = Path.Combine(_site.App.Environment.WebRootPath, url.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, Png(8), Ct);
        var media = new MediaItem { Id = id, Name = fileName, MediaType = MediaType.Image, Url = url };
        await _site.QueryDatabaseAsync(async db =>
        {
            db.Medias.Add(media);
            return await db.SaveChangesAsync(Ct);
        });
        return media;
    }

    private static byte[] Png(int size)
    {
        using var bitmap = new SKBitmap(size, size);
        bitmap.Erase(SKColors.CornflowerBlue);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private async Task<ZauberCMS.Core.Shared.Models.HandlerResult<MediaItem>> DeleteAsync(Guid id, bool permanently = false)
    {
        using var scope = _site.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IMediaService>().DeleteMediaAsync(
            new DeleteMediaParameters { MediaId = id, MoveToRecycleBin = !permanently }, Ct);
    }

    private sealed class PngBrowserFile(string name, byte[] content) : Microsoft.AspNetCore.Components.Forms.IBrowserFile
    {
        public string Name => name;
        public DateTimeOffset LastModified => DateTimeOffset.UtcNow;
        public long Size => content.Length;
        public string ContentType => "image/png";

        public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default) =>
            new MemoryStream(content);
    }
}
