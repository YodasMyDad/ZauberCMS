using Microsoft.AspNetCore.Components.Forms;

namespace ZauberCMS.Core.Shared.Models;

public class FileSaveResult
{
    public string? SavedFileUrl { get; set; }
    public IBrowserFile? OriginalFile { get; set; }
    public bool Success { get; set; } = true;
    public List<string> ErrorMessages { get; set; } = new();
    public int Height { get; set; }
    public int Width { get; set; }
}