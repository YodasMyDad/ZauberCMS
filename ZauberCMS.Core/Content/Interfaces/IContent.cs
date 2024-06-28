using ZauberCMS.Core.Content.Models;

namespace ZauberCMS.Core.Content.Interfaces;

public interface IContent<T>
{
    Guid Id { get; set; }
    string? Name { get; set; }
    string? Url { get; set; }
    Guid ContentTypeId { get; set; }
    int SortOrder { get; set; }
    bool IsRootContent { get; set; }
    Guid? InternalRedirectId { get; set; }
    Guid? ParentId { get; set; }
    DateTime DateCreated { get; set; }
    DateTime DateUpdated { get; set; }
    string ViewComponent { get; set; }
    List<T> PropertyData { get; set; }
}