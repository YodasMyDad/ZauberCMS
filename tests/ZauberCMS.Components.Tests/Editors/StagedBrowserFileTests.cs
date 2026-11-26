using Microsoft.AspNetCore.Components.Forms;
using ZauberCMS.Components.Editors.Models;

namespace ZauberCMS.Components.Tests.Editors;

/// <summary>
/// ZA-100: a file chosen in the upload editor is copied when it is chosen, so it can still be saved after the input's
/// next change (when Blazor's own file can no longer be read).
/// </summary>
public class StagedBrowserFileTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "ZauberCMS-tests", Guid.NewGuid().ToString("N"));

    public StagedBrowserFileTests() => StagedBrowserFile.StagingFolderForTests = _folder;

    public void Dispose()
    {
        StagedBrowserFile.StagingFolderForTests = null;
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private int CopiesOnDisk() => Directory.Exists(_folder) ? Directory.GetFiles(_folder).Length : 0;

    /// <summary>Like Blazor's file: readable until <see cref="Invalidate"/> (the input's next change).</summary>
    private sealed class BrowserFile(string name, byte[] content, int? failAfterBytes = null) : IBrowserFile
    {
        private bool _invalid;

        public string Name => name;
        public DateTimeOffset LastModified { get; } = DateTimeOffset.UtcNow;
        public long Size => content.Length;
        public string ContentType => "image/png";

        public void Invalidate() => _invalid = true;

        public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default)
        {
            if (_invalid)
            {
                throw new InvalidOperationException("The file is no longer available");
            }

            if (Size > maxAllowedSize)
            {
                throw new IOException("Too large");
            }

            return failAfterBytes is { } limit
                ? new FailingStream(content, limit)
                : new MemoryStream(content, writable: false);
        }
    }

    /// <summary>Gives <paramref name="limit"/> bytes, then fails, as a browser file that goes away mid-read does.</summary>
    private sealed class FailingStream(byte[] content, int limit) : MemoryStream(content, writable: false)
    {
        public override int Read(byte[] buffer, int offset, int count) =>
            Position >= limit ? throw new InvalidOperationException("Gone") : base.Read(buffer, offset, Math.Min(count, limit - (int)Position));

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            Position >= limit
                ? ValueTask.FromException<int>(new InvalidOperationException("Gone"))
                : base.ReadAsync(buffer[..Math.Min(buffer.Length, limit - (int)Position)], cancellationToken);
    }

    [Fact]
    public async Task A_staged_file_reads_after_the_browser_file_is_gone_and_its_copy_is_deleted_on_dispose()
    {
        var bytes = Enumerable.Range(0, 200_000).Select(i => (byte)i).ToArray();
        var browserFile = new BrowserFile("photo.png", bytes);

        var staged = await StagedBrowserFile.CreateAsync(browserFile, maxAllowedSize: 1_000_000, Ct);
        browserFile.Invalidate();

        Assert.Equal("photo.png", staged.Name);
        Assert.Equal("image/png", staged.ContentType);
        Assert.Equal(bytes.Length, staged.Size);
        Assert.Equal(1, CopiesOnDisk());

        // Readable twice, as the storage provider checks the first bytes and then saves the file
        for (var i = 0; i < 2; i++)
        {
            await using var stream = staged.OpenReadStream(bytes.Length, Ct);
            using var copy = new MemoryStream();
            await stream.CopyToAsync(copy, Ct);
            Assert.Equal(bytes, copy.ToArray());
        }

        // A smaller limit than the file throws, as Blazor's own file does
        Assert.Throws<IOException>(() => staged.OpenReadStream(bytes.Length - 1, Ct));

        staged.Dispose();
        Assert.Equal(0, CopiesOnDisk());
        staged.Dispose();
    }

    [Fact]
    public async Task A_file_over_the_limit_is_not_staged()
    {
        var browserFile = new BrowserFile("big.png", new byte[2_000]);

        await Assert.ThrowsAsync<IOException>(() => StagedBrowserFile.CreateAsync(browserFile, maxAllowedSize: 1_000, Ct));

        Assert.Equal(0, CopiesOnDisk());
    }

    [Fact]
    public async Task A_copy_that_fails_partway_leaves_no_file_behind()
    {
        var browserFile = new BrowserFile("half.png", new byte[300_000], failAfterBytes: 100_000);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            StagedBrowserFile.CreateAsync(browserFile, maxAllowedSize: 1_000_000, Ct));

        // The folder was made and written to, and the half copy deleted
        Assert.True(Directory.Exists(_folder));
        Assert.Equal(0, CopiesOnDisk());
    }
}
