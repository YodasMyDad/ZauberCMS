using ZauberCMS.Core.Extensions;

namespace ZauberCMS.Core.Shared.Models;

public class PropertyType
{
    public Guid Id { get; set; } = Guid.NewGuid().NewSequentialGuid();
    public string? Name { get; set; }
    public string? Alias { get; set; }
    public string? Description { get; set; }
    public string? Component { get; set; }
    public string? ComponentAlias { get; set; }
    public string? Settings { get; set; }
    public bool IsRequired { get; set; }
    public bool FullWidth { get; set; }
    public int SortOrder { get; set; }
    public Guid? TabId { get; set; }
    public bool ShowAlias { get; set; } = true;
}