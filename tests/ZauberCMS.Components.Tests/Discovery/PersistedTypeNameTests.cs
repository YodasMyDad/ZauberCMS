using ZauberCMS.Components.Editors;
using ZauberCMS.Core.Content.Interfaces;

namespace ZauberCMS.Components.Tests.Discovery;

/// <summary>
/// Type full names are stored data (AGENTS.md rule 2): a property type stores the full name of its editor
/// component. Renaming or moving an editor breaks every existing site, so the shipped names are pinned here.
/// </summary>
public class PersistedTypeNameTests
{
    private const string SnapshotFile = "PropertyEditors.txt";

    [Fact]
    public void Shipped_property_editor_type_names_have_not_changed()
    {
        var actual = typeof(TextProperty).Assembly.GetExportedTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false } && typeof(IContentProperty).IsAssignableFrom(t))
            .Select(t => t.FullName!)
            .Order(StringComparer.Ordinal)
            .ToList();

        var snapshotPath = Path.Combine(AppContext.BaseDirectory, "Discovery", "Snapshots", SnapshotFile);
        var expected = File.ReadAllLines(snapshotPath)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .ToList();

        var removed = expected.Except(actual).ToList();
        var added = actual.Except(expected).ToList();

        Assert.True(removed.Count == 0,
            "Property editors removed or renamed (this breaks existing sites, which store these names): " +
            string.Join(", ", removed));
        Assert.True(added.Count == 0,
            $"New property editors are not in Discovery/Snapshots/{SnapshotFile} yet. Add: " +
            string.Join(", ", added));
    }
}
