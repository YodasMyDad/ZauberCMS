using System.Xml.Linq;

namespace ZauberCMS.Core.Tests.Packaging;

/// <summary>
/// What the packages and the template give a site (Roadmap Phase 11c): no build files of this repository in a
/// package (ZA-087), the package page and debugging metadata (ZA-119), the framework scripts taken from the SDK
/// (ZA-094) and the template's own files (ZA-123). The files are read as text; <c>tests/template-smoke.ps1</c>
/// builds the packages and runs a site made from the template (CI runs it on every push).
/// </summary>
public class PackageContentsTests
{
    private static readonly string[] PackageProjects =
    [
        "ZauberCMS.Core/ZauberCMS.Core.csproj",
        "ZauberCMS.Components/ZauberCMS.Components.csproj",
        "ZauberCMS.Routing/ZauberCMS.Routing.csproj",
        "ZauberCMS/ZauberCMS.csproj",
    ];

    private const string TemplateFolder = "ZauberCMS.Template/template";
    private const string TemplateSite = TemplateFolder + "/ZauberCMSTemplate.Site/ZauberCMSTemplate.Site.csproj";

    [Fact]
    public void ZA087_the_npm_manifests_are_not_content_of_the_components_package()
    {
        var project = Load("ZauberCMS.Components/ZauberCMS.Components.csproj");

        // Content items are packed as contentFiles and land in every site that installs the package
        var removed = project.Descendants("Content")
            .SelectMany(c => ((string?)c.Attribute("Remove") ?? string.Empty).Split(';'))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Contains("package.json", removed);
        Assert.Contains("package-lock.json", removed);

        // No machine-specific path is left in any package project
        foreach (var path in PackageProjects)
        {
            Assert.DoesNotContain(@"\Users\", File.ReadAllText(Path.Combine(RepositoryRoot(), path)), StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void ZA119_the_packages_have_a_readme_an_icon_source_link_and_symbols()
    {
        var props = Load("Directory.Build.props");
        string Property(string name) => props.Descendants(name).Single().Value;

        Assert.Equal("ReadMe.md", Property("PackageReadmeFile"));
        Assert.Equal("logo.png", Property("PackageIcon"));
        Assert.Equal("true", Property("PublishRepositoryUrl"));
        Assert.Equal("true", Property("EmbedUntrackedSources"));
        Assert.Equal("true", Property("IncludeSymbols"));
        Assert.Equal("snupkg", Property("SymbolPackageFormat"));
        Assert.Equal("true", Property("Deterministic"));
        Assert.Contains("GITHUB_ACTIONS", (string)props.Descendants("ContinuousIntegrationBuild").Single().Attribute("Condition")!);

        // The files packed for them exist and are packed at the package root
        var packed = props.Descendants("None").Where(n => (string?)n.Attribute("Pack") == "true").ToList();
        foreach (var file in new[] { "ReadMe.md", "logo.png" })
        {
            var item = Assert.Single(packed, n => ((string)n.Attribute("Include")!).EndsWith(file, StringComparison.Ordinal));
            var path = ((string)item.Attribute("Include")!).Replace("$(MSBuildThisFileDirectory)", string.Empty, StringComparison.Ordinal);
            Assert.True(File.Exists(Path.Combine(RepositoryRoot(), path)), $"{path} does not exist");
        }
    }

    [Fact]
    public void ZA119_no_project_hides_obsolete_warnings()
    {
        foreach (var path in PackageProjects.Append("ZauberCMS.Web/ZauberCMS.Web.csproj").Append("Directory.Build.props"))
        {
            foreach (var noWarn in Load(path).Descendants("NoWarn"))
            {
                Assert.DoesNotContain("CS0618", noWarn.Value, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public void ZA119_a_package_does_not_repeat_the_packages_its_project_references_bring()
    {
        foreach (var path in PackageProjects)
        {
            var own = PackageReferences(path);
            var transitive = ReferencedProjects(path).SelectMany(PackageReferences).ToHashSet(StringComparer.OrdinalIgnoreCase);

            var repeated = own.Intersect(transitive, StringComparer.OrdinalIgnoreCase).ToList();
            Assert.True(repeated.Count == 0, $"{path} repeats {string.Join(", ", repeated)}");
        }
    }

    [Fact]
    public void ZA094_the_template_site_takes_the_framework_scripts_from_the_sdk()
    {
        var site = Load(TemplateSite);

        Assert.Equal("true", site.Descendants("RequiresAspNetWebAssets").Single().Value);
        Assert.DoesNotContain(site.Descendants("PackageReference"),
            r => ((string)r.Attribute("Include")!).StartsWith("Microsoft.AspNetCore.App.Internal", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ZA123_the_template_ships_a_gitignore_and_its_own_launch_profiles()
    {
        var root = RepositoryRoot();
        var gitignore = File.ReadAllText(Path.Combine(root, TemplateFolder, ".gitignore"));
        Assert.Contains("bin/", gitignore, StringComparison.Ordinal);
        Assert.Contains("*.db", gitignore, StringComparison.Ordinal);
        Assert.Contains("wwwroot/media/", gitignore, StringComparison.Ordinal);

        // NuGet leaves out files that start with a dot unless the project says otherwise
        var templatePackage = Load("ZauberCMS.Template/ZauberCMS.Template.csproj");
        Assert.Equal("true", templatePackage.Descendants("NoDefaultExcludes").Single().Value);

        var launchSettings = File.ReadAllText(Path.Combine(root, TemplateFolder, "ZauberCMSTemplate.Site/Properties/launchSettings.json"));
        Assert.DoesNotContain("Umbraco", launchSettings, StringComparison.OrdinalIgnoreCase);

        foreach (var program in new[] { "ZauberCMS.Web/Program.cs", TemplateFolder + "/ZauberCMSTemplate.Site/Program.cs" })
        {
            Assert.DoesNotContain("/Home/Error", File.ReadAllText(Path.Combine(root, program)), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void CI_runs_the_template_smoke_test()
    {
        var root = RepositoryRoot();
        Assert.True(File.Exists(Path.Combine(root, "tests/template-smoke.ps1")));

        var ci = File.ReadAllText(Path.Combine(root, ".github/workflows/ci.yml")).ReplaceLineEndings("\n");
        var build = ci.IndexOf("\n  build:\n", StringComparison.Ordinal);
        var publish = ci.IndexOf("\n  publish-nuget:\n", StringComparison.Ordinal);
        var smoke = ci.IndexOf("run: ./tests/template-smoke.ps1", StringComparison.Ordinal);

        // In the build job, which the publish job needs
        Assert.True(build > 0 && smoke > build && smoke < publish, "the build job runs tests/template-smoke.ps1");
    }

    private static HashSet<string> PackageReferences(string projectPath) =>
        Load(projectPath).Descendants("PackageReference")
            .Select(r => (string)r.Attribute("Include")!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    // The projects a project references, directly or through them, as paths from the repository root
    private static IEnumerable<string> ReferencedProjects(string projectPath)
    {
        var root = RepositoryRoot();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<string>([projectPath]);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            var folder = Path.GetDirectoryName(Path.Combine(root, current))!;
            foreach (var reference in Load(current).Descendants("ProjectReference"))
            {
                var full = Path.GetFullPath(Path.Combine(folder, ((string)reference.Attribute("Include")!).Replace('\\', '/')));
                var relative = Path.GetRelativePath(root, full).Replace('\\', '/');
                if (seen.Add(relative))
                {
                    queue.Enqueue(relative);
                }
            }
        }

        return seen;
    }

    private static XElement Load(string relativePath) =>
        XDocument.Load(Path.Combine(RepositoryRoot(), relativePath)).Root!;

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
