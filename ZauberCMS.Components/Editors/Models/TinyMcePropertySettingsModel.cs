namespace ZauberCMS.Components.Editors.Models;

public class TinyMcePropertySettingsModel
{
    public TinyMcePropertySettingsModel()
    {
        SelectedMenuBar = MenuBarList;
        SelectedToolBar = ToolBarList;
    }
    
    public bool ShowMenuBar { get; set; } = true;
    public List<string> SelectedMenuBar { get; set; }
    public List<string> SelectedToolBar { get; set; }
    
    public List<string> MenuBarList { get; set; } =
    [
        "file",
        "edit",
        "insert",
        "view",
        "format",
        "table",
        "tools"
    ];
    
    public List<string> ToolBarList { get; set; } =
    [
        "undo",
        "redo",
        "fontfamily",
        "fontsize",
        "bold",
        "italic",
        "underline",
        "strikethrough",
        "align",
        "numlist",
        "bullist",
        "link",
        "image",
        "table",
        "lineheight",
        "outdent",
        "indent",
        "forecolor",
        "backcolor",
        "removeformat",
        "charmap",
        "emoticons",
        "code",
        "fullscreen",
        "preview",
        "anchor",
        "codesample",
        "ltr",
        "rtl"
    ];
}