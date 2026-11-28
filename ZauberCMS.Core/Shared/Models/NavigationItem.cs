using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Membership.Validation;
using ZauberCMS.Core.Shared.Interfaces;

namespace ZauberCMS.Core.Shared.Models;

public class NavigationItem : ITreeItem
{
    public Guid Id { get; set; } = Guid.NewGuid().NewSequentialGuid();
    
    // The navigation editor's dialog shows these messages in the admin's language (D130)
    [ZauberRequired]
    public string? Name { get; set; }
    
    [ZauberRequired]
    public string? Url { get; set; }
    
    public List<NavigationItem> Children { get; set; } = [];
    public Guid? ContentId { get; set; }
    public int SortOrder { get; set; }
    public bool OpenInNewWindow { get; set; }
}