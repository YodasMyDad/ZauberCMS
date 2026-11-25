using System.Text;

namespace ZauberCMS.Core.Media;

/// <summary>
/// Checks that the first bytes of an upload are what its extension says. A file name is chosen by the uploader;
/// the content is what a browser or another program will act on.
/// </summary>
public static class UploadContentCheck
{
    /// <summary>How many leading bytes <see cref="Matches"/> wants to see.</summary>
    public const int HeadLength = 4096;

    private sealed record Signature(int Offset, byte[] Bytes);

    private static Signature At(int offset, params byte[] bytes) => new(offset, bytes);

    private static Signature Ascii(int offset, string text) => new(offset, Encoding.ASCII.GetBytes(text));

    /// <summary>For each extension: alternatives, each a set of byte runs that must all be present.</summary>
    private static readonly Dictionary<string, Signature[][]> Signatures = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = [[At(0, 0xFF, 0xD8, 0xFF)]],
        [".jpeg"] = [[At(0, 0xFF, 0xD8, 0xFF)]],
        [".png"] = [[At(0, 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A)]],
        [".gif"] = [[Ascii(0, "GIF87a")], [Ascii(0, "GIF89a")]],
        [".webp"] = [[Ascii(0, "RIFF"), Ascii(8, "WEBP")]],
        [".bmp"] = [[Ascii(0, "BM")]],
        [".tiff"] = [[At(0, 0x49, 0x49, 0x2A, 0x00)], [At(0, 0x4D, 0x4D, 0x00, 0x2A)]],
        [".ico"] = [[At(0, 0x00, 0x00, 0x01, 0x00)]],
        [".pdf"] = [[Ascii(0, "%PDF")]],
        // ISO base media files start with a box whose size varies; its type at offset 4 is always "ftyp".
        [".mp4"] = [[Ascii(4, "ftyp")]],
        [".m4a"] = [[Ascii(4, "ftyp")]],
        [".mov"] = [[Ascii(4, "ftyp")], [Ascii(4, "moov")], [Ascii(4, "mdat")], [Ascii(4, "wide")], [Ascii(4, "free")]],
        [".webm"] = [[At(0, 0x1A, 0x45, 0xDF, 0xA3)]],
        [".mkv"] = [[At(0, 0x1A, 0x45, 0xDF, 0xA3)]],
        [".mp3"] = [[Ascii(0, "ID3")], [At(0, 0xFF, 0xFB)], [At(0, 0xFF, 0xF3)], [At(0, 0xFF, 0xF2)], [At(0, 0xFF, 0xFA)]],
        [".wav"] = [[Ascii(0, "RIFF"), Ascii(8, "WAVE")]],
        [".avi"] = [[Ascii(0, "RIFF"), Ascii(8, "AVI ")]],
        [".ogg"] = [[Ascii(0, "OggS")]],
        [".flac"] = [[Ascii(0, "fLaC")]],
        [".zip"] = [[At(0, 0x50, 0x4B, 0x03, 0x04)], [At(0, 0x50, 0x4B, 0x05, 0x06)]],
        [".docx"] = [[At(0, 0x50, 0x4B, 0x03, 0x04)]],
        [".xlsx"] = [[At(0, 0x50, 0x4B, 0x03, 0x04)]],
        [".pptx"] = [[At(0, 0x50, 0x4B, 0x03, 0x04)]],
        [".odt"] = [[At(0, 0x50, 0x4B, 0x03, 0x04)]],
        [".ods"] = [[At(0, 0x50, 0x4B, 0x03, 0x04)]],
        [".odp"] = [[At(0, 0x50, 0x4B, 0x03, 0x04)]],
        [".doc"] = [[At(0, 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1)]],
        [".xls"] = [[At(0, 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1)]],
        [".ppt"] = [[At(0, 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1)]],
        [".rtf"] = [[Ascii(0, @"{\rtf")]],
        [".gz"] = [[At(0, 0x1F, 0x8B)]],
        [".7z"] = [[At(0, 0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C)]],
        [".rar"] = [[Ascii(0, "Rar!")]]
    };

    /// <summary>Extensions whose content must be plain text.</summary>
    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".md", ".log", ".csv", ".json", ".xml", ".svg"
    };

    /// <summary>
    /// True when <paramref name="head"/> (the first bytes of the file, up to <see cref="HeadLength"/>) fits the
    /// extension. Formats with a known signature must start with it. Text formats must be text: no binary
    /// content, and no HTML document or script in a file that claims to be plain text.
    /// An extension this class knows nothing about is accepted; the allowed-file-types list decides those.
    /// </summary>
    public static bool Matches(string extension, ReadOnlySpan<byte> head)
    {
        if (Signatures.TryGetValue(extension, out var alternatives))
        {
            foreach (var alternative in alternatives)
            {
                var all = true;
                foreach (var signature in alternative)
                {
                    if (head.Length < signature.Offset + signature.Bytes.Length ||
                        !head.Slice(signature.Offset, signature.Bytes.Length).SequenceEqual(signature.Bytes))
                    {
                        all = false;
                        break;
                    }
                }

                if (all)
                {
                    return true;
                }
            }

            return false;
        }

        if (TextExtensions.Contains(extension))
        {
            return IsText(head) && (IsMarkupExtension(extension) || !LooksLikeHtml(head));
        }

        return true;
    }

    private static bool IsMarkupExtension(string extension) =>
        extension.Equals(".xml", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".svg", StringComparison.OrdinalIgnoreCase);

    private static bool IsText(ReadOnlySpan<byte> head)
    {
        // UTF-16 text has a byte order mark and many zero bytes.
        if (head.Length >= 2 && ((head[0] == 0xFF && head[1] == 0xFE) || (head[0] == 0xFE && head[1] == 0xFF)))
        {
            return true;
        }

        foreach (var b in head)
        {
            // Control characters other than tab, line feed, form feed, carriage return and escape do not
            // occur in text files; a zero byte is the usual sign of a binary file.
            if (b < 0x20 && b is not (0x09 or 0x0A or 0x0C or 0x0D or 0x1B))
            {
                return false;
            }
        }

        return true;
    }

    private static bool LooksLikeHtml(ReadOnlySpan<byte> head)
    {
        var text = Encoding.UTF8.GetString(head).TrimStart('﻿', ' ', '\t', '\r', '\n');
        string[] markers = ["<!doctype html", "<html", "<head", "<body", "<script", "<iframe", "<svg", "<?xml"];
        return markers.Any(marker => text.StartsWith(marker, StringComparison.OrdinalIgnoreCase)) ||
               text.Contains("<script", StringComparison.OrdinalIgnoreCase);
    }
}
