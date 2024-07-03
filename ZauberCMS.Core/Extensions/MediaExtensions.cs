using ZauberCMS.Core.Media.Models;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Extensions;

public static class MediaExtensions
{
    /// <summary>
    /// Gets the media type based on the URL extension.
    /// </summary>
    /// <param name="url">The URL of the media file.</param>
    /// <returns>The media type of the file.</returns>
    public static MediaType GetMediaTypeFromUrl(this string url)
    {
        var extension = Path.GetExtension(url).ToLowerInvariant();

        return extension switch
        {
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".tiff" or ".svg" => MediaType.Image,
            ".mp4" or ".avi" or ".mov" or ".wmv" or ".flv" or ".mkv" or ".webm" or ".mpeg" => MediaType.Video,
            ".mp3" or ".wav" or ".aac" or ".flac" or ".ogg" or ".wma" or ".m4a" => MediaType.Audio,
            ".pdf" => MediaType.Pdf,
            ".doc" or ".docx" or ".rtf" or ".odt" => MediaType.Document,
            ".xls" or ".xlsx" or ".ods" or ".csv" => MediaType.Spreadsheet,
            ".ppt" or ".pptx" or ".odp" => MediaType.Presentation,
            ".zip" or ".rar" or ".7z" or ".tar" or ".gz" or ".bz2" => MediaType.Archive,
            ".txt" or ".md" or ".log" => MediaType.Text,
            _ => MediaType.Unknown
        };
    }

    /// <summary>
    /// Returns an icon based on the media type
    /// </summary>
    /// <param name="mediaType"></param>
    /// <returns></returns>
    public static string GetIcon(this MediaType mediaType)
    {
        return Icons.TryGetValue(mediaType, out var icon) ? icon : "question_mark";
    }

    private static readonly Dictionary<MediaType, string> Icons = new()
    {
        { MediaType.Folder, "folder" },
        { MediaType.Image, "image" },
        { MediaType.Video, "videocam" },
        { MediaType.Audio, "audiotrack" },
        { MediaType.Pdf, "picture_as_pdf" },
        { MediaType.Document, "article" },
        { MediaType.Spreadsheet, "storage" },
        { MediaType.Presentation, "trending_up" },
        { MediaType.Archive, "archive" },
        { MediaType.Text, "text_snippet" },
        { MediaType.Unknown, "question_mark" }
    };
}