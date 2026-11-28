using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ZauberCMS.Core.Email.Interfaces;
using ZauberCMS.Core.Email.Parameters;
using ZauberCMS.Core.Media.Interfaces;
using ZauberCMS.Core.Media.Models;
using ZauberCMS.Core.Media.Parameters;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Plugins;
using ZauberCMS.Core.Providers;
using ZauberCMS.Core.Settings;
using ZauberCMS.Core.Tests.Infrastructure;
using MediaItem = ZauberCMS.Core.Media.Models.Media;

namespace ZauberCMS.Core.Tests.Bootstrap;

/// <summary>
/// ZA-196 (Roadmap D83): a blank <c>Zauber:Plugins:StorageProvider</c> is a site that stores no files (a start-up
/// warning; an upload or a file delete is refused naming the setting) and a blank <c>EmailProvider</c> a site that
/// sends no email (no warning; a send fails naming the setting). Both threw <c>NullReferenceException</c>.
/// </summary>
public partial class BlankProviderTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ZA196_a_blank_storage_provider_refuses_an_upload_naming_the_setting()
    {
        await using var site = await TestSite.CreateAsync(Blank("StorageProvider"));
        using var scope = site.CreateScope();

        var result = await scope.ServiceProvider.GetRequiredService<IMediaService>().SaveMediaAsync(new SaveMediaParameters
        {
            FileToSave = new FakeBrowserFile("photo.png", [1, 2, 3]),
            MediaToSave = new MediaItem { Name = "Photo", MediaType = MediaType.Image }
        }, Ct);

        Assert.False(result.Success);
        Assert.Contains(result.Messages, m => m.Message == ProviderService.NoStorageProviderMessage);
        Assert.Contains("Zauber:Plugins:StorageProvider is empty", ProviderService.NoStorageProviderMessage,
            StringComparison.Ordinal);
        Assert.Equal(0, await site.QueryDatabaseAsync(db => Task.FromResult(db.Medias.Count())));
    }

    [Fact]
    public async Task ZA196_a_blank_storage_provider_refuses_a_permanent_delete_of_a_file_naming_the_setting()
    {
        await using var site = await TestSite.CreateAsync(Blank("StorageProvider"));
        var media = new MediaItem
        {
            Name = "Old", MediaType = MediaType.Image, Url = "media/old/old.png", Deleted = true
        };
        await site.QueryDatabaseAsync(async db =>
        {
            db.Medias.Add(media);
            return await db.SaveChangesAsync(Ct);
        });
        using var scope = site.CreateScope();

        var result = await scope.ServiceProvider.GetRequiredService<IMediaService>().DeleteMediaAsync(
            new DeleteMediaParameters { MediaId = media.Id, MoveToRecycleBin = false }, Ct);

        Assert.False(result.Success);
        Assert.Contains(result.Messages, m => m.Message == ProviderService.NoStorageProviderMessage);
        Assert.Equal(1, await site.QueryDatabaseAsync(db => Task.FromResult(db.Medias.Count())));
    }

    [Fact]
    public async Task ZA196_a_blank_email_provider_fails_a_send_naming_the_setting()
    {
        await using var site = await TestSite.CreateAsync(Blank("EmailProvider"));
        using var scope = site.CreateScope();
        scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = new DefaultHttpContext
        {
            Request = { Scheme = "https", Host = new HostString("example.test") },
            RequestServices = scope.ServiceProvider
        };

        // The confirmation email (registration, a changed address) and Identity's own sender
        var confirmation = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            scope.ServiceProvider.GetRequiredService<IEmailService>().SendEmailConfirmationAsync(
                new SendEmailConfirmationParameters { User = site.CurrentUser }, Ct));
        var reset = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            scope.ServiceProvider.GetRequiredService<IEmailSender<User>>().SendPasswordResetLinkAsync(
                site.CurrentUser, "test@example.test", "https://example.test/reset"));

        Assert.Equal(ProviderService.NoEmailProviderMessage, confirmation.Message);
        Assert.Equal(ProviderService.NoEmailProviderMessage, reset.Message);
        Assert.StartsWith("Email is not set up: Zauber:Plugins:EmailProvider is empty", reset.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task ZA196_the_required_providers_are_the_configured_ones_when_set()
    {
        await using var site = await TestSite.CreateAsync();
        using var scope = site.CreateScope();
        var providers = scope.ServiceProvider.GetRequiredService<ProviderService>();

        Assert.IsType<DiskStorageProvider>(providers.GetRequiredStorageProvider());
        Assert.IsType<SmtpEmailProvider>(providers.GetRequiredEmailProvider());
        Assert.Same(providers.StorageProvider, providers.GetRequiredStorageProvider());
    }

    [Fact]
    public async Task ZA196_the_required_providers_name_the_blank_setting()
    {
        await using var site = await TestSite.CreateAsync();
        using var scope = site.CreateScope();
        var providers = new ProviderService(Options.Create(new ZauberSettings
            {
                Plugins = { StorageProvider = " ", EmailProvider = "" }
            }),
            scope.ServiceProvider.GetRequiredService<ExtensionManager>());

        Assert.Null(providers.StorageProvider);
        Assert.Null(providers.EmailProvider);
        Assert.Equal(ProviderService.NoStorageProviderMessage,
            Assert.Throws<InvalidOperationException>(() => providers.GetRequiredStorageProvider()).Message);
        Assert.Equal(ProviderService.NoEmailProviderMessage,
            Assert.Throws<InvalidOperationException>(() => providers.GetRequiredEmailProvider()).Message);
    }

    [Fact]
    public async Task ZA196_a_blank_storage_provider_is_a_startup_warning_and_a_blank_email_provider_is_not()
    {
        await using var site = await TestSite.CreateAsync(); // the bootstrap sets the scanned assemblies
        using var services = new ServiceCollection().BuildServiceProvider();
        var extensionManager = new ExtensionManager(services);

        var noStorage = ZauberSettingsValidator.FindWarnings(
            new ZauberSettings { Plugins = { StorageProvider = "" } }, extensionManager).ToList();
        var noEmail = ZauberSettingsValidator.FindWarnings(
            new ZauberSettings { Plugins = { EmailProvider = "" } }, extensionManager).ToList();

        Assert.StartsWith("Zauber:Plugins:StorageProvider is empty", Assert.Single(noStorage), StringComparison.Ordinal);
        Assert.Empty(noEmail);
        // Neither stops the start (D48: blank is no provider)
        Assert.True(new ZauberSettingsValidator(services)
            .Validate(null, new ZauberSettings { Plugins = { StorageProvider = "", EmailProvider = "" } }).Succeeded);
    }

    [Fact]
    public void ZA196_no_code_dereferences_a_provider_that_may_be_null()
    {
        // StorageProvider and EmailProvider are null when their setting is blank: code that needs one calls
        // GetRequiredStorageProvider()/GetRequiredEmailProvider() or checks for null, never `!`
        var root = RepositoryRoot();
        var offenders = PackagedProjects
            .SelectMany(project => Directory.EnumerateFiles(Path.Combine(root, project), "*.*", SearchOption.AllDirectories))
            .Where(path => path.EndsWith(".cs", StringComparison.Ordinal) || path.EndsWith(".razor", StringComparison.Ordinal))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
                           !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .SelectMany(path => File.ReadLines(path).Select((line, index) => (path, line, index)))
            .Where(x => ForgivenProvider().IsMatch(x.line))
            .Select(x => $"{Path.GetRelativePath(root, x.path)}:{x.index + 1}")
            .ToList();

        Assert.True(offenders.Count == 0, "Dereferenced with '!': " + string.Join(", ", offenders));
    }

    private static readonly string[] PackagedProjects =
        ["ZauberCMS.Core", "ZauberCMS.Components", "ZauberCMS.Routing", "ZauberCMS"];

    [GeneratedRegex(@"\.(StorageProvider|EmailProvider)!")]
    private static partial Regex ForgivenProvider();

    private static Dictionary<string, string?> Blank(string provider) => new() { [$"Zauber:Plugins:{provider}"] = "" };

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "ZauberCMS.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("ZauberCMS.sln not found above the test output.");
    }

    private sealed class FakeBrowserFile(string name, byte[] content) : IBrowserFile
    {
        public string Name => name;
        public DateTimeOffset LastModified => DateTimeOffset.UtcNow;
        public long Size => content.Length;
        public string ContentType => "image/png";

        public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default) =>
            new MemoryStream(content);
    }
}
