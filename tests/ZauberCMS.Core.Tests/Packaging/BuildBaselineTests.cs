using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace ZauberCMS.Core.Tests.Packaging;

/// <summary>
/// The repository's build baseline (ZA-131) and CI hardening (ZA-089, ZA-109): shared build settings and central
/// package versions for the solution, a template that stays self-contained for `dotnet new` users, and a
/// workflow with pinned actions and least-privilege permissions. These files are read as text; CI itself is not
/// run by the tests.
/// </summary>
public partial class BuildBaselineTests
{
    private static readonly string[] PackageProjects =
    [
        "ZauberCMS.Core/ZauberCMS.Core.csproj",
        "ZauberCMS.Components/ZauberCMS.Components.csproj",
        "ZauberCMS.Routing/ZauberCMS.Routing.csproj",
        "ZauberCMS/ZauberCMS.csproj",
    ];

    private static readonly string[] SolutionProjects =
    [
        .. PackageProjects,
        "ZauberCMS.Web/ZauberCMS.Web.csproj",
        "tests/ZauberCMS.Core.Tests/ZauberCMS.Core.Tests.csproj",
        "tests/ZauberCMS.Components.Tests/ZauberCMS.Components.Tests.csproj",
        "tests/Directory.Build.props",
    ];

    private const string TemplateSite = "ZauberCMS.Template/template/ZauberCMSTemplate.Site/ZauberCMSTemplate.Site.csproj";

    [Fact]
    public void ZA131_solution_projects_take_package_versions_from_Directory_Packages_props()
    {
        var central = Load("Directory.Packages.props").Descendants("PackageVersion")
            .Select(p => (string)p.Attribute("Include")!).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var project in SolutionProjects)
        {
            foreach (var reference in Load(project).Descendants("PackageReference"))
            {
                var id = (string)reference.Attribute("Include")!;
                Assert.True(reference.Attribute("Version") == null, $"{project}: {id} sets a Version");
                Assert.True(central.Contains(id), $"{project}: {id} has no PackageVersion in Directory.Packages.props");
            }
        }
    }

    [Fact]
    public void ZA131_the_four_packages_share_one_version_in_Directory_Build_props()
    {
        Assert.Single(Load("Directory.Build.props").Descendants("Version"));
        foreach (var project in PackageProjects)
        {
            Assert.Empty(Load(project).Descendants("Version"));
        }
    }

    [Fact]
    public void ZA131_the_template_folder_stops_the_repository_build_files()
    {
        // MSBuild and NuGet use the nearest Directory.Build.props / Directory.Packages.props, so these two files keep
        // the repository's settings out of the template package and the template site.
        var buildProps = Load("ZauberCMS.Template/Directory.Build.props");
        Assert.Empty(buildProps.Elements());

        var packagesProps = Load("ZauberCMS.Template/Directory.Packages.props");
        Assert.Equal("false", packagesProps.Descendants("ManagePackageVersionsCentrally").Single().Value);
    }

    [Fact]
    public void ZA131_the_template_site_keeps_explicit_package_versions()
    {
        var references = Load(TemplateSite).Descendants("PackageReference").ToList();

        Assert.Contains(references, r => (string?)r.Attribute("Include") == "ZauberCMS");
        Assert.All(references, r => Assert.False(string.IsNullOrWhiteSpace((string?)r.Attribute("Version"))));
    }

    [Fact]
    public void ZA131_global_json_pins_the_sdk_band_and_keeps_the_test_runner()
    {
        var json = File.ReadAllText(Path.Combine(RepositoryRoot(), "global.json"));

        Assert.Matches(@"""version""\s*:\s*""10\.0\.\d00""", json);
        Assert.Contains(@"""rollForward"": ""latestFeature""", json);
        Assert.Contains(@"""runner"": ""Microsoft.Testing.Platform""", json);
    }

    [Fact]
    public void ZA109_workflow_actions_are_pinned_by_commit()
    {
        foreach (var workflow in Workflows())
        {
            var uses = UsesLine().Matches(File.ReadAllText(workflow)).Select(m => m.Groups["ref"].Value).ToList();

            Assert.NotEmpty(uses);
            Assert.All(uses, reference => Assert.Matches("^[0-9a-f]{40}$", reference));
        }
    }

    [Fact]
    public void ZA109_workflows_read_only_by_default()
    {
        foreach (var workflow in Workflows())
        {
            var text = File.ReadAllText(workflow).ReplaceLineEndings("\n");

            Assert.Contains("\npermissions:\n  contents: read\n", text);
        }
    }

    [Fact]
    public void ZA089_the_release_tag_is_pushed_before_any_package()
    {
        var text = File.ReadAllText(Path.Combine(RepositoryRoot(), ".github/workflows/ci.yml")).ReplaceLineEndings("\n");

        var tagPush = text.IndexOf("git push origin \"refs/tags/", StringComparison.Ordinal);
        var build = text.IndexOf("dotnet build ZauberCMS.sln -c Release\n", StringComparison.Ordinal);
        var packagePush = text.IndexOf("dotnet nuget push", StringComparison.Ordinal);

        Assert.True(tagPush > 0 && build > tagPush && packagePush > build,
            "the publish job must reserve the version's tag before it builds and pushes packages");
        Assert.Contains("for id in ZauberCMS.Core ZauberCMS.Components ZauberCMS.Routing ZauberCMS ZauberCMS.Template", text);
    }

    private static IEnumerable<string> Workflows() =>
        Directory.EnumerateFiles(Path.Combine(RepositoryRoot(), ".github", "workflows"), "*.yml");

    [GeneratedRegex(@"^\s*(?:-\s*)?uses:\s*[^@\s]+@(?<ref>\S+)", RegexOptions.Multiline)]
    private static partial Regex UsesLine();

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
