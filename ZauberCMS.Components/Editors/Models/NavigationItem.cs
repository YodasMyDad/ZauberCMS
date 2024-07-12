using System.ComponentModel.DataAnnotations;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Shared.Interfaces;

namespace ZauberCMS.Components.Editors.Models;

public class NavigationItem : ITreeItem
{
    public Guid Id { get; set; } = Guid.NewGuid().NewSequentialGuid();
    
    [Required]
    public string? Name { get; set; }
    
    [Required]
    public string? Url { get; set; }
    
    public List<NavigationItem> Children { get; set; } = [];
    public Guid? ContentId { get; set; }
    public int SortOrder { get; set; }
    public bool OpenInNewWindow { get; set; }
}