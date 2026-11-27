using System.Security.Cryptography;
using System.Text;
using ImageResize.Core.Configuration;
using ImageResize.Core.Interfaces;
using ImageResize.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ZauberCMS.Core.Media;

/// <summary>
/// Keeps the resized copies of each image in a folder of their own, so a permanent delete can remove them (Roadmap
/// D63, ZA-155). ImageResize names a copy by a hash of the image's path, the size asked for and the original's date
/// and length, which can't be traced back to the image; this cache puts that file in a folder named by a hash of the
/// image's path alone (<c>{CacheRoot}/{shards}/{path hash}/{copy}</c>, sharded by <c>Cache:FolderSharding</c>).
/// Reading, writing and the size limit stay ImageResize's: it walks the whole cache root.
/// </summary>
internal sealed class ResizedImageCache(IImageCache inner, IOptions<ImageResizeOptions> options) : IImageCache
{
    /// <inheritdoc />
    public string GetCachedFilePath(string relPath, ResizeOptions resizeOptions, string sourceSignature)
    {
        var copy = inner.GetCachedFilePath(relPath, resizeOptions, sourceSignature);
        return Path.Combine(FolderFor(options.Value, relPath), Path.GetFileName(copy));
    }

    /// <inheritdoc />
    public Task<bool> ExistsAsync(string cachedPath, CancellationToken ct = default) =>
        inner.ExistsAsync(cachedPath, ct);

    /// <inheritdoc />
    public Task<Stream> OpenReadAsync(string cachedPath, CancellationToken ct = default) =>
        inner.OpenReadAsync(cachedPath, ct);

    /// <inheritdoc />
    public Task WriteAtomicallyAsync(string cachedPath, Stream data, CancellationToken ct = default) =>
        inner.WriteAtomicallyAsync(cachedPath, data, ct);

    /// <summary>
    /// Removes every resized copy of the image at <paramref name="relativePath"/> (its path under the web root, as
    /// requested: <c>media/{id}/photo.jpg</c>). Nothing to remove is not a failure; a folder that can't be removed
    /// is logged, never thrown, since the original is what a delete is about.
    /// </summary>
    internal static void RemoveCopies(ImageResizeOptions options, string relativePath, ILogger logger)
    {
        string folder;
        try
        {
            folder = FolderFor(options, relativePath);
            if (!Directory.Exists(folder)) return;
            Directory.Delete(folder, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            logger.LogWarning(ex, "The resized copies of {Path} could not be removed from the image cache",
                relativePath);
        }
    }

    /// <summary>The folder that holds the resized copies of the image at <paramref name="relativePath"/>.</summary>
    internal static string FolderFor(ImageResizeOptions options, string relativePath)
    {
        // The form ImageResize's middleware passes (the request path without its leading slash), compared as it
        // compares paths: without case, either slash
        var normalised = relativePath.Replace('\\', '/').TrimStart('/').ToLowerInvariant();
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(normalised)), 0, 16);

        var parts = new List<string> { Path.GetFullPath(options.CacheRoot) };
        var levels = Math.Clamp(options.Cache.FolderSharding, 0, 4);
        for (var i = 0; i < levels; i++)
        {
            parts.Add(hash.Substring(i * 2, 2));
        }

        parts.Add(hash[(levels * 2)..]);
        return Path.Combine([.. parts]);
    }
}
