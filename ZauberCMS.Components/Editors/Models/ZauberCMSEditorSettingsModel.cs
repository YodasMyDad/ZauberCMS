namespace ZauberCMS.Components.Editors.Models;

public class ZauberCMSEditorSettingsModel
{
    // Dimensions
    public string Height { get; set; } = "300px";
    public string Width { get; set; } = "100%";
    public int MinHeight { get; set; } = 100;
    public int MaxHeight { get; set; } = 600;
    
    // Use CMS defaults or custom configuration
    public bool UseCmsDefaults { get; set; } = true;
    
    // Capabilities - only used when UseCmsDefaults = false
    // These match the actual EditorCapabilities properties
    public bool TextFormatting { get; set; } = true;
    public bool InteractiveElements { get; set; } = true;
    public bool EmbedsAndMedia { get; set; } = true;
    public bool Subscript { get; set; } = true;
    public bool Superscript { get; set; } = true;
    public bool TextAlign { get; set; } = true;
    public bool Underline { get; set; } = true;
    public bool Strike { get; set; } = true;
    public bool ClearFormatting { get; set; } = true;
    
    // Image constraints
    public int MaxImageWidth { get; set; } = 800;
    public int MaxImageHeight { get; set; } = 600;
    public bool MaintainAspectRatio { get; set; } = true;
    public bool AllowBase64ImageUpload { get; set; } = true;
}

