using ZauberCMS.Core.Content.Interfaces;
using ZauberCMS.Core.Content.Models;

namespace ZauberCMS.Components.Editors.Models;

public class TestingDataSource : IDataListSource
{
    public string Name => "Test Data";
    public string Description => "Example data list to make sure everything works";
    public string Icon => "query_stats";
    public string FullName => typeof(TestingDataSource).FullName ?? string.Empty;
    public IEnumerable<DataListItem> GetItems()
    {
                var items = new List<DataListItem>();

        foreach (var timezone in TimeZoneInfo.GetSystemTimeZones().Take(20))
        {
            items.Add(new DataListItem
            {
                Name = timezone.DisplayName,
                Value = timezone.Id
            });
        }

        return items;
    }
}