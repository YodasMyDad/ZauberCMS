namespace ZauberCMS.Components.Editors.Models;

public class CodeEditorPropertySettingsModel
{
    public int Height { get; set; } = 200;
    public string Language { get; set; } = "javascript";
    
    public List<string> Languages { get; set; } = [
        "csharp",
        "dart",
        "fsharp",
        "go",
        "html",
        "java",
        "javascript",
        "liquid",
        "markdown",
        "mysql",
        "pascal",
        "perl",
        "php",
        "powershell",
        "python",
        "razor",
        "ruby",
        "rust",
        "scala",
        "scss",
        "sql",
        "swift",
        "typescript",
        "xml",
        "yaml",
    ];
}