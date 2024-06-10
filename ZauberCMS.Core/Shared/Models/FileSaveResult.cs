using Microsoft.AspNetCore.Components.Forms;
using ZauberCMS.Core.Media.Models;

namespace ZauberCMS.Core.Shared.Models;

public class FileSaveResult
{
    public string? SavedFileUrl { get; set; }
    public IBrowserFile? OriginalFile { get; set; }
    public Media.Models.Media? SavedMedia { get; set; }
    public bool Success { get; set; } = true;
    public List<string> ErrorMessages { get; set; } = [];
    public MediaType MediaType { get; set; }
    public string? Name { get; set; }
    public int Height { get; set; }
    public int Width { get; set; }
}