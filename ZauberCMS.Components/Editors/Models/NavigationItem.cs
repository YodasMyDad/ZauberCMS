using ZauberCMS.Core.Extensions;

namespace ZauberCMS.Components.Editors.Models;

public class NavigationItem
{
    public Guid Id { get; set; } = Guid.NewGuid().NewSequentialGuid();
    public string? Name { get; set; }
    public string? Url { get; set; }
    public List<NavigationItem> Children { get; set; } = [];
    public Guid? ContentId { get; set; }
    public int SortOrder { get; set; }
    public bool OpenInNewWindow { get; set; }
}