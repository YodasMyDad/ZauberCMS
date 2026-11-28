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

    [Fact]
    public void ZA178_the_publish_job_stops_a_rerun_of_a_commit_older_than_the_newest_release()
    {
        var text = File.ReadAllText(Path.Combine(RepositoryRoot(), ".github/workflows/ci.yml")).ReplaceLineEndings("\n");
        var publish = text[text.IndexOf("\n  publish-nuget:\n", StringComparison.Ordinal)..];

        // A rerun of a released commit finds its tag first and publishes it again (D44); any other run checks that
        // the newest version tag is in its history before it reserves a version
        var rerun = publish.IndexOf("git tag --points-at HEAD", StringComparison.Ordinal);
        var ancestry = publish.IndexOf("git merge-base --is-ancestor", StringComparison.Ordinal);
        var tagPush = publish.IndexOf("git push origin \"refs/tags/", StringComparison.Ordinal);
        Assert.True(rerun > 0 && ancestry > rerun && tagPush > ancestry,
            "the publish job must check that HEAD contains the newest release tag after the rerun check and before it reserves a version");
        Assert.Contains("fetch-depth: 0", publish, StringComparison.Ordinal);
    }

    [Fact]
    public void ZA195_the_commit_back_replays_the_bump_on_the_newest_main_static_without_overwriting_a_persons_change()
    {
        var text = File.ReadAllText(Path.Combine(RepositoryRoot(), ".github/workflows/ci.yml")).ReplaceLineEndings("\n");
        var start = text.IndexOf("- name: Commit the stamped version back to main-static", StringComparison.Ordinal);
        Assert.True(start > text.IndexOf("dotnet nuget push", StringComparison.Ordinal),
            "the version is committed back after the packages are pushed");
        var step = text[start..];

        // A plain `pull --rebase` conflicted with the bump of the release before when two pushes were in flight
        Assert.DoesNotContain("git pull --rebase", step, StringComparison.Ordinal);
        var fetch = step.IndexOf("git fetch --quiet origin +refs/heads/main-static:refs/remotes/origin/main-static",
            StringComparison.Ordinal);
        var rolledBack = step.IndexOf("if ! git merge-base --is-ancestor \"${built}\" \"${tip}\"; then",
            StringComparison.Ordinal);
        var newerKept = step.IndexOf("if is_higher \"${current}\" \"${VERSION}\"", StringComparison.Ordinal);
        var personsKept = step.IndexOf("main-static has later changes to the version files", StringComparison.Ordinal);
        var replay = step.IndexOf("git rebase --quiet -X theirs \"${tip}\"", StringComparison.Ordinal);
        var push = step.IndexOf("if git push origin HEAD:main-static; then", StringComparison.Ordinal);
        Assert.True(fetch > 0 && rolledBack > fetch && newerKept > rolledBack && personsKept > newerKept &&
                    replay > personsKept && push > replay,
            "each attempt fetches main-static, stops when it no longer holds the commit built (ZA-233), keeps a " +
            "newer version and a person's change, then replays the bump and pushes");
        // Only this job's own bumps are overridden, and a lost race is tried again
        Assert.Contains("$2 == bot && $3 ~ /^Bump version to [^ ]+ \\[skip ci\\]$/", step, StringComparison.Ordinal);
        Assert.Contains("for attempt in 1 2 3 4 5; do", step, StringComparison.Ordinal);
    }

    [Fact]
    public void ZA180_vulnerability_warnings_and_an_unreadable_audit_are_not_build_errors()
    {
        var props = Load("Directory.Build.props");
        Assert.Equal("true", props.Descendants("TreatWarningsAsErrors").Single().Value);
        var notAsErrors = props.Descendants("WarningsNotAsErrors").Single().Value.Split(';');

        // NU1900: the vulnerability data could not be read (offline, or the feed failing); NU1901-NU1904: advisories
        foreach (var code in new[] { "NU1900", "NU1901", "NU1902", "NU1903", "NU1904" })
        {
            Assert.Contains(code, notAsErrors);
        }

        // CI's own scan still fails on a vulnerable package or a scan problem
        var ci = File.ReadAllText(Path.Combine(RepositoryRoot(), ".github/workflows/ci.yml"));
        Assert.Contains("--vulnerable --include-transitive --format json", ci, StringComparison.Ordinal);
        Assert.Contains("grep -q '\"problems\"' vulnerable-packages.json", ci, StringComparison.Ordinal);
    }

    [Fact]
    public void ZA177_the_release_script_stamps_files_that_exist_and_has_no_control_characters_in_its_text()
    {
        var text = File.ReadAllText(Path.Combine(RepositoryRoot(), "release.ps1"));

        // A TAB is indentation only: "\t" in a double-quoted PowerShell string is not an escape, so a typed one was
        // a TAB in a path. Other control characters (but CR and LF) never belong in the script.
        foreach (var (line, number) in text.Split('\n').Select((l, i) => (l.TrimEnd('\r'), i + 1)))
        {
            var bad = line.TrimStart(' ', '\t').Where(char.IsControl).Select(c => $"U+{(int)c:X4}").ToList();
            Assert.True(bad.Count == 0, $"release.ps1:{number} has control characters in its text: {string.Join(", ", bad)}");
        }

        var stamps = StampedFile().Matches(text);
        Assert.True(stamps.Count >= 3, "release.ps1 stamps Directory.Build.props and the template's two csprojs");
        foreach (Match stamp in stamps)
        {
            var path = Path.Combine([RepositoryRoot(), .. stamp.Groups["path"].Value.Split('\\', '/')]);
            Assert.True(File.Exists(path), $"release.ps1 stamps {stamp.Groups["path"].Value}, which does not exist");
            Assert.True(Regex.IsMatch(File.ReadAllText(path), stamp.Groups["pattern"].Value),
                $"release.ps1's pattern {stamp.Groups["pattern"].Value} finds nothing in {stamp.Groups["path"].Value}");
        }

        var props = Regex.Match(text, "-PropsPath \"(?<path>[^\"]+)\"").Groups["path"].Value;
        Assert.True(File.Exists(Path.Combine(RepositoryRoot(), props)), $"release.ps1 reads {props}, which does not exist");
    }

    // Update-VersionInFile -Path "<file>" `<newline> -Pattern '<regex>'
    [GeneratedRegex(@"Update-VersionInFile\s+-Path\s+""(?<path>[^""]+)""\s*`?\s*-Pattern\s+'(?<pattern>[^']+)'")]
    private static partial Regex StampedFile();

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
