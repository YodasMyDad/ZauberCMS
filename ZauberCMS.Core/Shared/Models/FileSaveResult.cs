using Microsoft.AspNetCore.Components.Forms;
using ZauberCMS.Core.Media.Models;

namespace ZauberCMS.Core.Shared.Models;

public class FileSaveResult
{
    public Guid? CurrentMediaId { get; set; }
    public string? SavedFileUrl { get; set; }
    public IBrowserFile? OriginalFile { get; set; }
    public Media.Models.Media? SavedMedia { get; set; }
    public bool Success { get; set; } = true;
    public List<string> ErrorMessages { get; set; } = [];
    public MediaType? MediaType { get; set; }
    public string? Name { get; set; }
    public long Height { get; set; }
    public long Width { get; set; }
    
    public long? FileSize { get; set; }
}