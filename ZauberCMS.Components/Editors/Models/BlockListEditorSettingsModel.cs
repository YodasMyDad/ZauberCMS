namespace ZauberCMS.Components.Editors.Models;

public class BlockListEditorSettingsModel
{
    public List<string> Styleheets { get; set; } = [];
    public IEnumerable<Guid> AllowedElementTypeIds { get; set; } = [];
}