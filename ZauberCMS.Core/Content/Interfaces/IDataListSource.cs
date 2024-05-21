using ZauberCMS.Core.Content.Models;

namespace ZauberCMS.Core.Content.Interfaces;

public interface IDataListSource
{
    string Name { get; }
    string Description { get; }
    string Icon { get; }
    string FullName { get; }
    IEnumerable<DataListItem> GetItems();
}