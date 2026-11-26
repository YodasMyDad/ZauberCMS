using Microsoft.AspNetCore.Components.Forms;

namespace ZauberCMS.Components.Editors.Models;

/// <summary>
/// A file someone chose in the browser, copied to a temporary file on the server when it was chosen (as Umbraco keeps
/// uploads as temporary files until the media is saved). Blazor's own <see cref="IBrowserFile"/> can only be read
/// until the input's next change, so files chosen in two goes could not all be saved (ZA-100). Dispose to delete the
/// copy. A plugin's own upload editor can use it the same way.
/// </summary>
public sealed class StagedBrowserFile : IBrowserFile, IDisposable
{
    private const int BufferSize = 81920;
    private readonly string _path;

    private StagedBrowserFile(string path, IBrowserFile source, long size)
    {
        _path = path;
        Name = source.Name;
        LastModified = source.LastModified;
        ContentType = source.ContentType;
        Size = size;
    }

    public string Name { get; }
    public DateTimeOffset LastModified { get; }
    public long Size { get; }
    public string ContentType { get; }

    /// <summary>The folder the copies go in, under the system temp folder.</summary>
    internal static string StagingFolder => StagingFolderForTests ?? Path.Combine(Path.GetTempPath(), "ZauberCMS", "uploads");

    /// <summary>Tests use a folder of their own, so a site uploading at the same time can't change what they count.</summary>
    internal static string? StagingFolderForTests { get; set; }

    /// <summary>Copies <paramref name="file"/> to a temporary file, reading at most <paramref name="maxAllowedSize"/> bytes.</summary>
    public static async Task<StagedBrowserFile> CreateAsync(IBrowserFile file, long maxAllowedSize,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(StagingFolder);
        var path = Path.Combine(StagingFolder, Guid.NewGuid().ToString("N"));
        try
        {
            long size;
            await using (var source = file.OpenReadStream(maxAllowedSize, cancellationToken))
            await using (var target = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                             BufferSize, useAsync: true))
            {
                await source.CopyToAsync(target, BufferSize, cancellationToken);
                size = target.Length;
            }

            return new StagedBrowserFile(path, file, size);
        }
        catch
        {
            TryDelete(path);
            throw;
        }
    }

    /// <summary>Reads the copy. Like Blazor's own files, a file larger than <paramref name="maxAllowedSize"/> throws.</summary>
    public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default)
    {
        if (Size > maxAllowedSize)
        {
            throw new IOException($"Supplied file with size {Size} bytes exceeds the maximum of {maxAllowedSize} bytes.");
        }

        return new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, useAsync: true);
    }

    public void Dispose() => TryDelete(_path);

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // Still open somewhere, or already gone: the system temp folder is cleaned up anyway
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
