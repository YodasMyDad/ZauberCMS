using System.Text;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ZauberCMS.Core.Media;
using ZauberCMS.Core.Providers;
using ZauberCMS.Core.Settings;
using ZauberCMS.Core.Shared.Services;
using ZauberCMS.Core.Tests.Infrastructure;

namespace ZauberCMS.Core.Tests.Security;

/// <summary>
/// Content that comes from users and is later shown by a browser: rich text (ZA-021), uploaded files (ZA-022)
/// and SVG images (ZA-005).
/// </summary>
public class UploadAndMarkupTests
{
    private const string PngDataUrl = "data:image/png;base64,iVBORw0KGgo=";

    private static readonly DefaultHtmlSanitizerService Sanitizer = new();

    // ---------- rich text ----------

    [Theory]
    [InlineData("https://www.youtube.com/embed/abc123")]
    [InlineData("https://www.youtube-nocookie.com/embed/abc123")]
    [InlineData("https://player.vimeo.com/video/123")]
    public void An_iframe_from_an_allowed_video_host_is_kept(string source)
    {
        var html = Sanitizer.Sanitize($"""<p>Watch</p><iframe src="{source}" width="560" height="315" allowfullscreen></iframe>""");

        Assert.Contains("<iframe", html);
        Assert.Contains(source, html);
    }

    [Theory]
    [InlineData("https://evil.example/page")]
    [InlineData("http://www.youtube.com/embed/abc123")] // not https
    [InlineData("https://www.youtube.com.evil.example/embed/abc")]
    [InlineData("//www.youtube.com/embed/abc123")]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html;base64,PHNjcmlwdD5hbGVydCgxKTwvc2NyaXB0Pg==")]
    [InlineData("")]
    public void An_iframe_from_anywhere_else_is_removed_and_the_rest_is_kept(string source)
    {
        var html = Sanitizer.Sanitize($"""<p>Before</p><iframe src="{source}"></iframe><p>After</p>""");

        Assert.DoesNotContain("iframe", html);
        Assert.Contains("<p>Before</p>", html);
        Assert.Contains("<p>After</p>", html);
    }

    [Fact]
    public void An_iframe_with_inline_content_is_removed()
    {
        var html = Sanitizer.Sanitize("""<iframe srcdoc="<script>alert(1)</script>"></iframe>""");

        Assert.DoesNotContain("iframe", html);
        Assert.DoesNotContain("script", html);
    }

    [Fact]
    public void The_allowed_iframe_hosts_come_from_settings()
    {
        var custom = new DefaultHtmlSanitizerService(Options.Create(new ZauberSettings
        {
            HtmlSanitizer = new HtmlSanitizerSettings { AllowedIframeHosts = ["maps.example"] }
        }));

        Assert.Contains("<iframe", custom.Sanitize("""<iframe src="https://maps.example/embed"></iframe>"""));
        Assert.DoesNotContain("iframe", custom.Sanitize("""<iframe src="https://www.youtube.com/embed/abc"></iframe>"""));
    }

    [Fact]
    public void A_bitmap_data_address_is_kept_on_an_image()
    {
        Assert.Contains(PngDataUrl, Sanitizer.Sanitize($"""<img src="{PngDataUrl}" alt="dot">"""));
    }

    [Theory]
    [InlineData("""<a href="data:text/html;base64,PHNjcmlwdD5hbGVydCgxKTwvc2NyaXB0Pg==">x</a>""")]
    [InlineData("""<img src="data:image/svg+xml;base64,PHN2ZyBvbmxvYWQ9YWxlcnQoMSk+">""")]
    [InlineData("""<img src="data:text/html,<script>alert(1)</script>">""")]
    [InlineData("""<a href="data:image/png;base64,iVBORw0KGgo=">x</a>""")]
    [InlineData("""<div style="background:url(data:text/html;base64,PHNjcmlwdD4=)">x</div>""")]
    public void Any_other_data_address_is_removed(string input)
    {
        Assert.DoesNotContain("data:", Sanitizer.Sanitize(input));
    }

    [Fact]
    public void Scripts_and_event_handlers_are_removed_and_ordinary_formatting_is_kept()
    {
        var html = Sanitizer.Sanitize(
            """<h2 class="title" style="color: red">Hi</h2><script>alert(1)</script><img src="/media/a.png" onerror="alert(1)" data-id="7"><a href="https://example.com">link</a>""");

        Assert.DoesNotContain("script", html);
        Assert.DoesNotContain("onerror", html);
        Assert.Contains("""class="title""", html);
        Assert.Contains("style=\"color:", html);
        Assert.Contains("""data-id="7""", html);
        Assert.Contains("""href="https://example.com""", html);
    }

    // ---------- upload content ----------

    public static TheoryData<string, byte[], bool> UploadCases => new()
    {
        { ".png", [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0], true },
        { ".png", Encoding.UTF8.GetBytes("<html><script>alert(1)</script></html>"), false },
        { ".jpg", [0xFF, 0xD8, 0xFF, 0xE0], true },
        { ".pdf", Encoding.ASCII.GetBytes("%PDF-1.7"), true },
        { ".pdf", Encoding.ASCII.GetBytes("MZ this is a program"), false },
        // An MP4 starts with a box of any size; its type "ftyp" is at offset 4.
        { ".mp4", [0x00, 0x00, 0x00, 0x14, 0x66, 0x74, 0x79, 0x70, 0x69, 0x73, 0x6F, 0x6D], true },
        { ".mp4", Encoding.ASCII.GetBytes("<html>not a video</html>"), false },
        { ".webp", Encoding.ASCII.GetBytes("RIFF\0\0\0\0WEBPVP8 "), true },
        { ".webp", Encoding.ASCII.GetBytes("RIFF\0\0\0\0WAVEfmt "), false },
        { ".txt", Encoding.UTF8.GetBytes("Plain notes.\r\nSecond line with a < b and ümlauts."), true },
        { ".txt", [], true },
        { ".txt", Encoding.UTF8.GetBytes("<!DOCTYPE html><html><body>hi</body></html>"), false },
        { ".txt", Encoding.UTF8.GetBytes("  \r\n<script>alert(1)</script>"), false },
        { ".txt", Encoding.UTF8.GetBytes("notes then <ScRiPt src=//evil.example></script>"), false },
        { ".txt", [0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00], false }, // a Windows program
        { ".svg", Encoding.UTF8.GetBytes("""<svg xmlns="http://www.w3.org/2000/svg"></svg>"""), true },
        { ".svg", [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00], false },
        { ".xyz", [0x00, 0x01, 0x02], true } // unknown to the check: the allowed-file-types list decides
    };

    [Theory]
    [MemberData(nameof(UploadCases))]
    public void The_first_bytes_of_an_upload_must_fit_its_extension(string extension, byte[] head, bool expected)
    {
        Assert.Equal(expected, UploadContentCheck.Matches(extension, head));
    }

    [Theory]
    [InlineData("notes.txt", "Just some notes.", true)]
    [InlineData("notes.txt", "<html><script>alert(1)</script></html>", false)]
    [InlineData("photo.png", "<html><script>alert(1)</script></html>", false)]
    [InlineData("page.html", "<html></html>", false)] // not an allowed file type at all
    public async Task The_storage_provider_applies_the_check_to_uploads(string fileName, string content, bool expected)
    {
        await using var site = await TestSite.CreateAsync();
        using var scope = site.CreateScope();
        var provider = ActivatorUtilities.CreateInstance<DiskStorageProvider>(scope.ServiceProvider);

        var result = await provider.CanUseFile(new FakeBrowserFile(fileName, Encoding.UTF8.GetBytes(content)));

        Assert.Equal(expected, result.Success);
    }

    // ---------- SVG ----------

    [Fact]
    public void A_plain_drawing_survives_sanitising()
    {
        var svg = Sanitize(
            """
            <svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" viewBox="0 0 10 10">
              <defs><linearGradient id="g"><stop offset="0" stop-color="#f00"/></linearGradient></defs>
              <style>.a { fill: url(#g); }</style>
              <rect class="a" width="10" height="10" fill="url(#g)"/>
              <use xlink:href="#g"/>
              <image href="data:image/png;base64,iVBORw0KGgo=" width="1" height="1"/>
              <text x="1" y="5">Hi</text>
            </svg>
            """);

        Assert.Contains("<rect", svg);
        Assert.Contains("linearGradient", svg);
        Assert.Contains("url(#g)", svg);
        Assert.Contains("#g", svg);
        Assert.Contains("data:image/png;base64", svg);
        Assert.Contains(">Hi<", svg);
        Assert.Contains("viewBox=\"0 0 10 10\"", svg);
    }

    [Theory]
    [InlineData("""<script>alert(1)</script>""", "script")]
    [InlineData("""<script xlink:href="https://evil.example/x.js"/>""", "evil.example")]
    [InlineData("""<rect width="1" height="1" onclick="alert(1)"/>""", "onclick")]
    [InlineData("""<g onload="alert(1)"><rect width="1" height="1"/></g>""", "onload")]
    [InlineData("""<foreignObject><body xmlns="http://www.w3.org/1999/xhtml"><iframe src="https://evil.example"/></body></foreignObject>""", "evil.example")]
    [InlineData("""<a href="javascript:alert(1)"><text>x</text></a>""", "javascript")]
    [InlineData("""<a xlink:href="https://evil.example"><text>x</text></a>""", "evil.example")]
    [InlineData("""<image href="https://evil.example/track.png"/>""", "evil.example")]
    [InlineData("""<image href="data:image/svg+xml;base64,PHN2Zz4="/>""", "data:image/svg")]
    [InlineData("""<use href="https://evil.example/sprite.svg#a"/>""", "evil.example")]
    [InlineData("""<rect width="1" height="1" fill="url(https://evil.example/f.svg#a)"/>""", "evil.example")]
    [InlineData("""<rect width="1" height="1" style="background:url('https://evil.example/t.png')"/>""", "evil.example")]
    [InlineData("""<style>@import url(https://evil.example/s.css); rect { fill: red }</style>""", "evil.example")]
    [InlineData("""<style>rect { background: url("https://evil.example/t.png") }</style>""", "evil.example")]
    [InlineData("""<a id="l"><set attributeName="href" to="javascript:alert(1)"/><text>x</text></a>""", "javascript")]
    [InlineData("""<animate xlink:href="#l" attributeName="xlink:href" values="javascript:alert(1)"/>""", "javascript")]
    [InlineData("""<handler xmlns:ev="http://www.w3.org/2001/xml-events" ev:event="load">alert(1)</handler>""", "alert")]
    public void Script_handlers_and_outside_references_are_removed_from_an_SVG(string dangerous, string mustNotRemain)
    {
        var svg = Sanitize(
            $"""<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink"><circle r="4"/>{dangerous}</svg>""");

        Assert.DoesNotContain(mustNotRemain, svg, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<circle", svg);
    }

    [Fact]
    public void A_document_type_with_entities_is_not_expanded_or_kept()
    {
        var svg = Sanitize(
            """
            <?xml version="1.0"?>
            <?xml-stylesheet href="https://evil.example/s.css"?>
            <!DOCTYPE svg [ <!ENTITY xxe SYSTEM "file:///c:/windows/win.ini"> ]>
            <svg xmlns="http://www.w3.org/2000/svg"><circle r="4"/></svg>
            """.Trim());

        Assert.DoesNotContain("DOCTYPE", svg);
        Assert.DoesNotContain("ENTITY", svg);
        Assert.DoesNotContain("evil.example", svg);
        Assert.Contains("<circle", svg);
    }

    [Theory]
    [InlineData("<html><body>not svg</body></html>")]
    [InlineData("""<svg><circle r="4"/></svg>""")] // no SVG namespace: browsers would not draw it either
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\"><circle")]
    [InlineData("just text")]
    public void Something_that_is_not_an_SVG_document_is_refused(string input)
    {
        Assert.False(SvgSanitizer.TrySanitize(new MemoryStream(Encoding.UTF8.GetBytes(input)), out _));
    }

    [Fact]
    public async Task An_uploaded_SVG_is_stored_sanitised()
    {
        await using var site = await TestSite.CreateAsync();
        using var scope = site.CreateScope();
        var dataService = scope.ServiceProvider.GetRequiredService<ZauberCMS.Core.Data.Interfaces.IDataService>();
        var globalSettings = new GlobalSettings();
        globalSettings.AllowedFileTypes.Add(".svg"); // SVG is off by default; a site owner opts in
        Assert.True(await ZauberCMS.Core.Extensions.ServiceExtensions.SaveGlobalSettings(dataService, globalSettings));
        var provider = ActivatorUtilities.CreateInstance<DiskStorageProvider>(scope.ServiceProvider);
        var upload = new FakeBrowserFile("logo.svg", Encoding.UTF8.GetBytes(
            """<svg xmlns="http://www.w3.org/2000/svg" onload="alert(1)"><script>alert(2)</script><circle r="4"/></svg>"""));

        var saved = await provider.SaveFile(upload);
        var notSvg = await provider.SaveFile(new FakeBrowserFile("fake.svg", Encoding.UTF8.GetBytes("<html><script>alert(1)</script></html>")));

        Assert.True(saved.Success, TestSite.Describe(saved.Messages));
        var webRoot = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>().WebRootPath;
        var stored = await File.ReadAllTextAsync(Path.Combine(webRoot, saved.Entity!.Url!), TestContext.Current.CancellationToken);
        Assert.Contains("<circle", stored);
        Assert.DoesNotContain("script", stored);
        Assert.DoesNotContain("onload", stored);
        Assert.False(notSvg.Success);
    }

    private static string Sanitize(string svg)
    {
        Assert.True(SvgSanitizer.TrySanitize(new MemoryStream(Encoding.UTF8.GetBytes(svg)), out var sanitized));
        return sanitized;
    }

    private sealed class FakeBrowserFile(string name, byte[] content) : IBrowserFile
    {
        public string Name => name;
        public DateTimeOffset LastModified => DateTimeOffset.UtcNow;
        public long Size => content.Length;
        public string ContentType => "application/octet-stream";

        public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default) =>
            new MemoryStream(content);
    }
}
