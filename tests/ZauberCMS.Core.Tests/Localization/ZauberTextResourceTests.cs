using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using ZauberCMS.Components.Editors;
using ZauberCMS.Core.Localization;

namespace ZauberCMS.Core.Tests.Localization;

/// <summary>
/// The guard of Roadmap Phase 10h (D37, D94): ZauberCMS's text resources and the code that asks for them stay in
/// step. Each key is named after its file (<c>General.resx</c> holds <c>general_*</c>) and is in one file only;
/// every key the code asks for (a literal <c>L["area_name"]</c> / <c>text["area_name"]</c>) is in the English
/// files (Core's keys in Core's own); every English text is in the built assemblies; and the folders already
/// converted keep no hard-coded text. The files are read from the repository (Docs/AdminUI.md "Localisation").
/// </summary>
public partial class ZauberTextResourceTests
{
    private const string CoreProject = "ZauberCMS.Core";
    private const string ComponentsProject = "ZauberCMS.Components";
    private static readonly string[] Projects = [CoreProject, ComponentsProject];

    // The folders whose text is converted: their files may not keep hard-coded text. Not recursive: each folder is
    // listed. Each part of Phase 10h adds its folders in its own block, so the parts merge without touching each
    // other's lines.
    private static readonly string[] ConvertedFolders =
    [
        // 10h-1
        "ZauberCMS.Components/Admin/Dashboards",
        "ZauberCMS.Components/Admin/Shared",
        "ZauberCMS.Components/Admin/Shared/Dialogs",
        "ZauberCMS.Components/ContentComponents",
        "ZauberCMS.Components/ContextMenus",
        "ZauberCMS.Components/ContextMenus/ContentMenus",
        "ZauberCMS.Components/ContextMenus/MediaMenus",
        "ZauberCMS.Components/ContextMenus/MediaRecycleBinMenus",
        "ZauberCMS.Components/ContextMenus/RecycleBinMenus",
        "ZauberCMS.Components/ContextMenus/SectionNavGroups",
        "ZauberCMS.Components/ContextMenus/SettingsMenus",
        "ZauberCMS.Components/ContextMenus/Structure",
        "ZauberCMS.Components/Core/Content/Interfaces",
        "ZauberCMS.Components/Core/Extensions",
        "ZauberCMS.Components/Core/Media/Services",
        "ZauberCMS.Components/Core/Sections/Interfaces",
        "ZauberCMS.Components/Core/Shared/Services",
        "ZauberCMS.Components/DataSources",
        "ZauberCMS.Components/Modal",
        "ZauberCMS.Components/Modal/Configuration",
        "ZauberCMS.Components/Modal/Services",
        "ZauberCMS.Components/Shared",
        "ZauberCMS.Components/Startup",
        "ZauberCMS.Components/Trees",

        // 10h-2

        // 10h-3

        // 10h-4
    ];

    // Text in a converted folder that is not for a reader to read, "file: text" (the file relative to the repository).
    private static readonly HashSet<string> NotText =
    [
        // 10h-1
        "ZauberCMS.Components/Admin/Shared/ExtendedDataEditor.razor: remove", // a Material icon's name

        // 10h-2

        // 10h-3

        // 10h-4
    ];

    // Keys written ahead of the code that uses them (the part that converts that code takes its line out).
    private static readonly HashSet<string> NotUsedYet =
    [
        // 10h-1 for 10h-4 (Admin/UsersSection is converted by 10h-4)
        "sections_users",
        "sections_usersGroup",
    ];

    [Fact]
    public void D94_each_key_is_named_after_its_file_and_is_in_one_file_once()
    {
        var problems = new List<string>();
        var owners = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var file in ResourceFiles())
        {
            var prefix = char.ToLowerInvariant(file.Area[0]) + file.Area[1..] + "_";
            foreach (var group in file.Entries.GroupBy(e => e.Key).Where(g => g.Count() > 1))
            {
                problems.Add($"{file.Path}: {group.Key} is in the file {group.Count()} times");
            }

            foreach (var (key, _) in file.Entries)
            {
                if (!key.StartsWith(prefix, StringComparison.Ordinal) || !KeyName().IsMatch(key))
                {
                    problems.Add($"{file.Path}: {key} must be {prefix}camelCaseName");
                }

                if (file.Culture is null && !owners.TryAdd(key, file.Path))
                {
                    problems.Add($"{key} is in {owners[key]} and {file.Path}");
                }
            }
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void D94_texts_are_trimmed_and_numbered_from_zero_and_a_translation_matches_its_English()
    {
        var files = ResourceFiles().ToList();
        var english = files.Where(f => f.Culture is null).SelectMany(f => f.Entries)
            .ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal);
        var problems = new List<string>();
        foreach (var file in files)
        {
            foreach (var (key, value) in file.Entries)
            {
                if (value.Length == 0 || value != value.Trim())
                {
                    problems.Add($"{file.Path}: {key} is empty or starts or ends with white space");
                }

                var placeholders = Placeholders(value);
                if (placeholders.Count > 0 && placeholders.Max() != placeholders.Count - 1)
                {
                    problems.Add($"{file.Path}: {key}'s placeholders are not {{0}} to {{{placeholders.Count - 1}}}");
                }

                if (file.Culture is null)
                {
                    continue;
                }

                if (!english.TryGetValue(key, out var source))
                {
                    problems.Add($"{file.Path}: {key} is not in the English files");
                }
                else if (!Placeholders(source).SetEquals(placeholders))
                {
                    problems.Add($"{file.Path}: {key} has other placeholders than its English");
                }
            }
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void D94_every_key_the_code_asks_for_is_in_the_English_files_and_every_English_key_is_used()
    {
        var files = ResourceFiles().Where(f => f.Culture is null).ToList();
        var coreKeys = files.Where(f => f.Project == CoreProject).SelectMany(f => f.Entries).Select(e => e.Key).ToHashSet();
        var allKeys = files.SelectMany(f => f.Entries).Select(e => e.Key).ToHashSet();
        var used = new HashSet<string>(StringComparer.Ordinal);
        var problems = new List<string>();
        foreach (var project in Projects)
        {
            foreach (var (path, file) in SourceFiles(project))
            {
                // Keys in comments are examples
                var source = Blank(Blank(file, RazorComment()), CommentLine());
                foreach (Match match in KeyUse().Matches(source))
                {
                    var key = match.Groups["key"].Value;
                    used.Add(key);
                    // Core runs without Components' files (its tests, a site without the admin)
                    var known = project == CoreProject ? coreKeys : allKeys;
                    if (!known.Contains(key))
                    {
                        problems.Add($"{path}:{LineOf(source, match.Index)}: {key} is not in the English files" +
                                     (project == CoreProject ? " of ZauberCMS.Core" : string.Empty));
                    }
                }
            }
        }

        // General is the shared vocabulary: its keys are there for the parts that use them
        problems.AddRange(files.Where(f => f.Area != "General")
            .SelectMany(f => f.Entries.Select(e => (f.Path, e.Key)))
            .Where(e => !used.Contains(e.Key) && !NotUsedYet.Contains(e.Key))
            .Select(e => $"{e.Path}: {e.Key} is not used by any code"));
        problems.AddRange(NotUsedYet.Where(used.Contains).Select(k => $"{k} is used now: take it out of NotUsedYet"));

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void D94_every_English_text_is_in_the_built_assemblies()
    {
        var core = typeof(ZauberText).Assembly;
        var components = typeof(TextProperty).Assembly;
        var catalog = ZauberTextCatalog.FromAssemblies([core, components], [core, components], NullLoggerFactory.Instance);

        var missing = ResourceFiles().Where(f => f.Culture is null).SelectMany(f => f.Entries)
            .Where(e => catalog.Find(e.Key, CultureInfo.InvariantCulture) != e.Value)
            .Select(e => e.Key)
            .ToList();

        Assert.True(missing.Count == 0, "Not in the built assemblies as written in the files: " + string.Join(", ", missing));
    }

    [Fact]
    public void D94_the_converted_folders_keep_no_hard_coded_text()
    {
        var problems = new List<string>();
        foreach (var folder in ConvertedFolders)
        {
            var directory = Path.Combine(RepositoryRoot(), folder);
            Assert.True(Directory.Exists(directory), $"{folder} does not exist");
            foreach (var file in Directory.EnumerateFiles(directory).Where(IsSource).Order(StringComparer.Ordinal))
            {
                var relative = Path.GetRelativePath(RepositoryRoot(), file).Replace('\\', '/');
                var source = File.ReadAllText(file);
                var found = file.EndsWith(".razor", StringComparison.Ordinal) ? RazorText(source) : CodeText(source);
                problems.AddRange(found
                    .Where(t => !NotText.Contains($"{relative}: {t.Text}"))
                    .Select(t => $"{relative}:{t.Line}: {t.Text}"));
            }
        }

        Assert.True(problems.Count == 0,
            "Hard-coded text in a converted folder; use L[\"area_name\"] (Docs/AdminUI.md \"Localisation\"), or add " +
            "it to NotText when it is not for a reader:" + Environment.NewLine + string.Join(Environment.NewLine, problems));
    }

    // Markup text and text attributes, then the @code block as C#
    private static IEnumerable<(int Line, string Text)> RazorText(string source)
    {
        var markup = Blank(source, RazorComment());
        markup = Blank(markup, HtmlComment());
        markup = Blank(markup, ScriptOrStyle());
        var code = markup.IndexOf("@code", StringComparison.Ordinal);
        var codeBlock = string.Empty;
        if (code >= 0)
        {
            // Keep the lines of the markup before it; the C# check reads the rest
            codeBlock = new string(markup.Select((c, i) => i < code && c != '\n' ? ' ' : c).ToArray());
            markup = new string(markup.Select((c, i) => i >= code && c != '\n' ? ' ' : c).ToArray());
        }

        foreach (Match match in TextNode().Matches(markup))
        {
            var text = Entity().Replace(match.Groups["text"].Value, " ").Trim();
            if (text.IndexOfAny(['@', '{', '}', '(', ')', ';', '=']) < 0 && Word().IsMatch(text))
            {
                yield return (LineOf(markup, match.Index), text);
            }
        }

        foreach (Match match in TextAttribute().Matches(markup))
        {
            var text = match.Groups["text"].Value;
            if (!text.Contains('@', StringComparison.Ordinal) && Word().IsMatch(text))
            {
                yield return (LineOf(markup, match.Index), text);
            }
        }

        foreach (var found in CodeText(codeBlock))
        {
            yield return found;
        }
    }

    // A literal given to a UI member or call, or any literal that reads like a sentence, outside logs and exceptions
    private static IEnumerable<(int Line, string Text)> CodeText(string source)
    {
        var lines = source.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line.StartsWith("//", StringComparison.Ordinal) || line.StartsWith("/*", StringComparison.Ordinal) ||
                line.StartsWith('*') || line.StartsWith('[') || NotForReaders().IsMatch(line))
            {
                continue;
            }

            foreach (Match match in UiLiteral().Matches(line))
            {
                if (Word().IsMatch(match.Groups["text"].Value))
                {
                    yield return (i + 1, match.Groups["text"].Value);
                }
            }

            foreach (Match match in Sentence().Matches(line))
            {
                if (!UiLiteral().IsMatch(line))
                {
                    yield return (i + 1, match.Groups["text"].Value);
                }
            }
        }
    }

    private static string Blank(string source, Regex pattern) =>
        pattern.Replace(source, m => new string(m.Value.Select(c => c == '\n' ? '\n' : ' ').ToArray()));

    private static HashSet<int> Placeholders(string value) =>
        Placeholder().Matches(value.Replace("{{", string.Empty, StringComparison.Ordinal))
            .Select(m => int.Parse(m.Groups["index"].Value, CultureInfo.InvariantCulture)).ToHashSet();

    private static int LineOf(string source, int index) => source.AsSpan(0, index).Count('\n') + 1;

    private static bool IsSource(string path) =>
        path.EndsWith(".razor", StringComparison.Ordinal) || path.EndsWith(".cs", StringComparison.Ordinal);

    private static IEnumerable<(string Path, string Source)> SourceFiles(string project)
    {
        var root = Path.Combine(RepositoryRoot(), project);
        return Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
            .Where(IsSource)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
                        !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Select(p => (Path.GetRelativePath(RepositoryRoot(), p).Replace('\\', '/'), File.ReadAllText(p)));
    }

    private sealed record ResourceFile(string Path, string Project, string Area, string? Culture,
        IReadOnlyList<(string Key, string Value)> Entries);

    // ZauberCMS.Components/Resources/ZauberText/General.resx, General.fr.resx
    private static IEnumerable<ResourceFile> ResourceFiles()
    {
        foreach (var project in Projects)
        {
            var folder = Path.Combine(RepositoryRoot(), project, "Resources", ZauberText.ResourceFolder);
            foreach (var path in Directory.EnumerateFiles(folder, "*.resx").Order(StringComparer.Ordinal))
            {
                var name = Path.GetFileNameWithoutExtension(path).Split('.');
                var entries = XDocument.Load(path).Root!.Elements("data")
                    .Select(d => ((string)d.Attribute("name")!, (string?)d.Element("value") ?? string.Empty))
                    .ToList();
                yield return new ResourceFile(Path.GetRelativePath(RepositoryRoot(), path).Replace('\\', '/'), project,
                    name[0], name.Length > 1 ? name[1] : null, entries);
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

    [GeneratedRegex("^[a-z][A-Za-z0-9]*_[a-z][A-Za-z0-9]*$")]
    private static partial Regex KeyName();

    // L["general_save"], text["contextMenus_delete", name], Localizer["seo_title"]
    [GeneratedRegex("""\[\s*"(?<key>[a-z][A-Za-z0-9]*_[a-z][A-Za-z0-9]*)"\s*[,\]]""")]
    private static partial Regex KeyUse();

    [GeneratedRegex(@"\{(?<index>\d+)(?:[,:][^}]*)?\}")]
    private static partial Regex Placeholder();

    [GeneratedRegex(@"@\*.*?\*@", RegexOptions.Singleline)]
    private static partial Regex RazorComment();

    [GeneratedRegex(@"^\s*(?://|\*|/\*).*$", RegexOptions.Multiline)]
    private static partial Regex CommentLine();

    [GeneratedRegex("<!--.*?-->", RegexOptions.Singleline)]
    private static partial Regex HtmlComment();

    [GeneratedRegex(@"<(script|style)\b.*?</\1>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex ScriptOrStyle();

    [GeneratedRegex("&#?[A-Za-z0-9]+;")]
    private static partial Regex Entity();

    [GeneratedRegex(">(?<text>[^<>]+)<")]
    private static partial Regex TextNode();

    [GeneratedRegex(@"\b(?:Text|Title|Placeholder|Label|EmptyText|title|placeholder|aria-label|alt)=""(?<text>[^""]*)""")]
    private static partial Regex TextAttribute();

    [GeneratedRegex("[A-Za-z]{2,}")]
    private static partial Regex Word();

    // Lines whose literals are for logs, exceptions and attributes
    [GeneratedRegex(@"\bLog(?:Trace|Debug|Information|Warning|Error|Critical)?\(|\bthrow\b|Exception\(|\bnameof\(|\[Obsolete|\[Description")]
    private static partial Regex NotForReaders();

    [GeneratedRegex(@"(?:\b(?:Summary|Detail|Text|Title|OkButtonText|CancelButtonText|Heading|Name|TabName|Description|Placeholder)\s*(?:=>|=)\s*|\b(?:Show\w*Notifications?|Confirm|Alert|OpenSidePanel<[^>()]+>|Show<[^>()]+>)\(\s*)\$?""(?<text>(?:[^""\\]|\\.)*)""")]
    private static partial Regex UiLiteral();

    [GeneratedRegex(@"\$?""(?<text>[A-Z][a-z]+(?: [^""]+)+)""")]
    private static partial Regex Sentence();
}
