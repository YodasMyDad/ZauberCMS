using System.Text.Json;

namespace ZauberCMS.Core.Tests.Packaging;

/// <summary>
/// The "Zauber" settings section exists in three files that must not drift apart: the dev site, the site the
/// template scaffolds, and the defaults packed into the ZauberCMS NuGet package.
/// </summary>
public class AppSettingsParityTests
{
    private const string DevSite = "ZauberCMS.Web/appsettings.json";
    private const string TemplateSite = "ZauberCMS.Template/template/ZauberCMSTemplate.Site/appsettings.json";
    private const string PackagedDefaults = "ZauberCMS/appsettings.json";

    [Fact]
    public void Template_site_and_dev_site_have_the_same_Zauber_setting_keys()
    {
        var dev = ZauberKeys(DevSite);
        var template = ZauberKeys(TemplateSite);

        Assert.Empty(dev.Except(template));
        Assert.Empty(template.Except(dev));
    }

    [Fact]
    public void Packaged_defaults_contain_no_key_the_template_site_lacks()
    {
        Assert.Empty(ZauberKeys(PackagedDefaults).Except(ZauberKeys(TemplateSite)));
    }

    [Fact(Skip = "ZA-118 (open): the packaged appsettings.json has no Zauber:ImageResize section.")]
    public void Packaged_defaults_have_every_key_the_template_site_has()
    {
        Assert.Empty(ZauberKeys(TemplateSite).Except(ZauberKeys(PackagedDefaults)));
    }

    private static SortedSet<string> ZauberKeys(string relativePath)
    {
        var path = Path.Combine(RepositoryRoot(), relativePath);
        using var document = JsonDocument.Parse(File.ReadAllText(path),
            new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });

        var keys = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        Collect(document.RootElement.GetProperty("Zauber"), string.Empty, keys);
        return keys;
    }

    private static void Collect(JsonElement element, string prefix, SortedSet<string> keys)
    {
        foreach (var property in element.EnumerateObject())
        {
            var key = prefix + property.Name;
            if (property.Value.ValueKind == JsonValueKind.Object && property.Value.EnumerateObject().Any())
            {
                Collect(property.Value, key + ":", keys);
            }
            else
            {
                keys.Add(key);
            }
        }
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "ZauberCMS.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("ZauberCMS.sln not found above the test output.");
    }
}
