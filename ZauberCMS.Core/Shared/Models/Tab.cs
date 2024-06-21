namespace ZauberCMS.Core.Shared.Models;

public class Tab
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsSystemTab { get; set; }
}