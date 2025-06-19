namespace ZauberCMS.Components.Editors.Models;

public class RichTextEditorSettingsModel
{
    public bool AllowUndo { get; set; } = true;
    public bool AllowRedo { get; set; } = true;
    
    // seperator
    
    public bool AllowBold { get; set; } = true;
    public bool AllowItalic { get; set; } = true;
    public bool AllowUnderline { get; set; } = true;
    public bool AllowStrikeThrough { get; set; } = true;
    
    // seperator
    
    public bool AllowAlignLeft { get; set; } = true;
    public bool AllowAlignCenter { get; set; } = true;
    public bool AllowAlignRight { get; set; } = true;
    public bool AllowJustify { get; set; } = true;
    public bool AllowIndent { get; set; } = true;
    public bool AllowOutdent { get; set; } = true;
    
    // seperator
    
    public bool AllowUnorderedList { get; set; } = true;
    public bool AllowOrderedList { get; set; } = true;
    
    // seperator
    
    public bool AllowColor { get; set; } = true;
    public bool AllowBackground { get; set; } = true;
    public bool AllowRemoveFormat { get; set; } = true;
    public bool AllowSubscript { get; set; } = true;
    public bool AllowSourceEdit { get; set; } = true;
    public bool AllowSuperscript { get; set; } = true;
    // seperator
    public bool AllowLink { get; set; } = true;
    public bool AllowUnlink { get; set; } = true;
    public bool AllowImages { get; set; } = true;
    
    // seperator
    public bool AllowFontName { get; set; } = true;
    public bool AllowFontSize { get; set; } = true;
    public bool AllowFormatBlock { get; set; } = true;
}

//public bool ShowSeparator { get; set; } = true;