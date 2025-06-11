using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Shared.Interfaces;

namespace ZauberCMS.Core.Audit.Models;

public class Audit : ITreeItem
{
    public Guid Id { get; set; } = Guid.NewGuid().NewSequentialGuid();
    public DateTime DateCreated { get; set; } = DateTime.UtcNow;
    public DateTime DateUpdated { get; set; } = DateTime.UtcNow;
    public string? Description { get; set; }
    public string? Name
    {
        get => Description;
        set => Description = value;
    }
}