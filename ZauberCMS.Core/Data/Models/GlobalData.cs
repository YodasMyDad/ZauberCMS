using ZauberCMS.Core.Extensions;

namespace ZauberCMS.Core.Data.Models;

public class GlobalData
{
    public Guid Id { get; set; } = Guid.NewGuid().NewSequentialGuid();
    public string? Alias { get; set; }
    
    public string? Data { get; set; }
    public DateTime DateUpdated { get; set; } = DateTime.UtcNow;
}