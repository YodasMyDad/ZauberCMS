namespace ZauberCMS.Components.Editors.Models;

public class ZauberCMSEditorSettingsModel
{
    // Optional initial height
    public string? Height { get; set; }
    
    // Toolbar layout: "Full" or "Minimal"
    public string ToolbarLayout { get; set; } = "Full";
    
    // Image constraints
    public bool AllowBase64ImageUpload { get; set; } = false;
}