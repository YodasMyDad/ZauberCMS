using ZauberCMS.Core.Shared.Interfaces;

namespace ZauberCMS.Core.Content.Interfaces;

public interface IContent<T> : IBaseItem
{
    List<T> PropertyData { get; set; }
}