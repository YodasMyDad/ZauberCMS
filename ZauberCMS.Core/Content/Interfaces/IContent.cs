using ZauberCMS.Core.Shared.Interfaces;

namespace ZauberCMS.Core.Content.Interfaces;

public interface IContent<T> : IBaseItem
{
    List<T> PropertyData { get; set; }
}

public interface IBaseItem : ITreeItem
{
    string? Url { get; set; }
    int SortOrder { get; set; }
    Guid? ParentId { get; set; }
    DateTime DateCreated { get; set; }
    DateTime DateUpdated { get; set; }
}