namespace ZauberCMS.Core.Shared.Interfaces;

public interface IBaseItem : ITreeItem
{
    string? Url { get; set; }
    int SortOrder { get; set; }
    Guid? ParentId { get; set; }
    DateTime DateCreated { get; set; }
    DateTime DateUpdated { get; set; }
    List<Guid> Path { get; set; }
}