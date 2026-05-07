using System.Text.Json.Serialization;

namespace ZauberCMS.Components.Editors.Models;

public class BlockListEditorSettingsModel
{
    /// <summary>
    /// CSS stylesheet paths (relative to website root, e.g. "/css/styles.css") loaded into each
    /// block preview's shadow root and as &lt;link&gt; elements above the editor.
    /// </summary>
    [JsonPropertyName("Stylesheets")]
    public List<string> Stylesheets { get; set; } = [];

    /// <summary>
    /// Back-compat shim for settings JSON saved under the original misspelled key "Styleheets".
    /// The deserialiser populates this property, which migrates the value into <see cref="Stylesheets"/>.
    /// Never serialised back out (getter returns null), so saves write the corrected key only.
    /// Once a record is re-saved it stops carrying the legacy key.
    /// </summary>
    [JsonPropertyName("Styleheets")]
    [JsonInclude]
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public List<string>? StyleheetsLegacy
    {
        get => null;
        set
        {
            if (value is { Count: > 0 } && Stylesheets.Count == 0)
            {
                Stylesheets = value;
            }
        }
    }

    public IEnumerable<Guid> AllowedElementTypeIds { get; set; } = [];
}