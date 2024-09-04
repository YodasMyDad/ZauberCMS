namespace ZauberCMS.Components.Editors.Models;

public class EditorNotesSettingsModel
{
    /// <summary>
    /// Optional background colour override
    /// </summary>
    public string? BackgroundColor { get; set; }
    
    /// <summary>
    /// Title
    /// </summary>
    public string? Title { get; set; }
    
    /// <summary>
    /// Main note to editor
    /// </summary>
    public string? Note { get; set; }

    /// <summary>
    /// Optional icon override
    /// </summary>
    public string? Icon { get; set; } = "lightbulb_outline";
    
    /// <summary>
    /// Defaults to dialog, but can be made to show inline
    /// </summary>
    public bool ShowInline { get; set; }
}