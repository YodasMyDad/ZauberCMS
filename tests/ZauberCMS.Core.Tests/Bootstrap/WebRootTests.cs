using ImageResize.Core.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;

namespace ZauberCMS.Core.Tests.Bootstrap;

/// <summary>
/// ZA-217 (Roadmap D100): ASP.NET Core leaves the web root empty when the content root has no <c>wwwroot</c>
/// folder, and ImageResize's options then failed the start with an <c>ArgumentNullException</c> (<c>path1</c>).
/// <c>AddZauberCms(builder)</c> gives such a host <c>wwwroot</c> in its content root, created, as Umbraco falls back to
/// it; a folder that cannot be created stops the start naming it.
/// </summary>
public class WebRootTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ZA217_a_site_without_a_wwwroot_folder_starts_with_one_made_in_its_content_root()
    {
        var contentRoot = NewContentRoot();
        var webRoot = Path.Combine(contentRoot, "wwwroot");
        var builder = CreateBuilder(contentRoot);
        Assert.True(string.IsNullOrEmpty(builder.Environment.WebRootPath)); // as ASP.NET Core leaves it

        builder.AddZauberCms();
        await using var app = builder.Build();
        // ImageResize validates its options at start, where the web root was null
        await app.StartAsync(Ct);
        try
        {
            Assert.Equal(webRoot, app.Environment.WebRootPath);
            Assert.True(Directory.Exists(webRoot));
            var options = app.Services.GetRequiredService<IOptions<ImageResizeOptions>>().Value;
            Assert.Equal(webRoot, options.WebRoot);
            Assert.Equal(Path.Combine(webRoot, "_mediacache"), options.CacheRoot);

            // Served as the web root: a file written there later (an upload, a sitemap) is found
            await File.WriteAllTextAsync(Path.Combine(webRoot, "sitemap.xml"), "<urlset/>", Ct);
            Assert.True(app.Environment.WebRootFileProvider.GetFileInfo("sitemap.xml").Exists);
        }
        finally
        {
            await app.StopAsync(Ct);
            TryDelete(contentRoot);
        }
    }

    [Fact]
    public void ZA217_static_web_assets_the_host_already_serves_are_kept_behind_the_new_web_root()
    {
        // In Development UseStaticWebAssets wraps the empty web root provider with the packages' assets
        var contentRoot = NewContentRoot();
        var assets = Path.Combine(contentRoot, "assets");
        Directory.CreateDirectory(assets);
        File.WriteAllText(Path.Combine(assets, "package.js"), "//");
        using var packageAssets = new PhysicalFileProvider(assets);
        var environment = new WebHostEnvironment
        {
            ContentRootPath = contentRoot,
            WebRootFileProvider = new CompositeFileProvider(new NullFileProvider(), packageAssets)
        };

        ZauberSetup.EnsureWebRoot(environment);
        File.WriteAllText(Path.Combine(contentRoot, "wwwroot", "site.css"), "/**/");

        Assert.Equal(Path.Combine(contentRoot, "wwwroot"), environment.WebRootPath);
        Assert.True(environment.WebRootFileProvider.GetFileInfo("site.css").Exists);
        Assert.True(environment.WebRootFileProvider.GetFileInfo("package.js").Exists);
        TryDelete(contentRoot);
    }

    [Fact]
    public void ZA217_a_web_root_that_cannot_be_created_stops_the_start_naming_the_folder()
    {
        var contentRoot = NewContentRoot();
        var webRoot = Path.Combine(contentRoot, "wwwroot");
        // A file where the folder would go: no web root for ASP.NET Core, and no folder can be made there
        File.WriteAllText(webRoot, "not a folder");
        var builder = CreateBuilder(contentRoot);

        var error = Assert.Throws<InvalidOperationException>(() => builder.AddZauberCms());

        Assert.Contains($"'{webRoot}' could not be created", error.Message, StringComparison.Ordinal);
        Assert.IsAssignableFrom<IOException>(error.InnerException);
        TryDelete(contentRoot);
    }

    [Fact]
    public void ZA217_a_web_root_the_host_has_is_kept()
    {
        var contentRoot = NewContentRoot();
        var own = Path.Combine(contentRoot, "public");
        Directory.CreateDirectory(own);
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Testing",
            ContentRootPath = contentRoot,
            WebRootPath = own
        });
        UseNoDatabase(builder);

        builder.AddZauberCms();

        Assert.Equal(own, builder.Environment.WebRootPath);
        Assert.False(Directory.Exists(Path.Combine(contentRoot, "wwwroot")));
        TryDelete(contentRoot);
    }

    private static WebApplicationBuilder CreateBuilder(string contentRoot)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Testing",
            ContentRootPath = contentRoot
        });
        UseNoDatabase(builder);
        return builder;
    }

    // No database: the site would show the installer, which is all a start needs
    private static void UseNoDatabase(WebApplicationBuilder builder)
    {
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Zauber:DatabaseProvider"] = "",
            ["Zauber:ConnectionString"] = "",
            ["urls"] = "http://127.0.0.1:0"
        });
    }

    private static string NewContentRoot()
    {
        var contentRoot = Path.Combine(Path.GetTempPath(), "zauber-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(contentRoot);
        return contentRoot;
    }

    private sealed class WebHostEnvironment : Microsoft.AspNetCore.Hosting.IWebHostEnvironment
    {
        public string WebRootPath { get; set; } = null!;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ApplicationName { get; set; } = "Test";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = "Development";
    }

    private static void TryDelete(string directory)
    {
        try
        {
            Directory.Delete(directory, recursive: true);
        }
        catch (IOException)
        {
            // A file still open on Windows: the temp folder is cleaned up later
        }
        catch (UnauthorizedAccessException)
        {
            // As above
        }
    }
}
