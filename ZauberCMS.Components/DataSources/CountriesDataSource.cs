using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Content.Interfaces;
using ZauberCMS.Core.Content.Models;

namespace ZauberCMS.Components.DataSources;

public class CountriesDataSource : IDataListSource
{
    public string Name => "Countries Data";
    public string Description => "Country data list in English in alphabetical order";
    public string Icon => "flag";
    public string FullName => GetType().FullName ?? string.Empty;
    public IEnumerable<DataListItem> GetItems(IServiceScope scope, Content? currentContent)
    {
        var cultures = CultureInfo.GetCultures(CultureTypes.SpecificCultures);
        var items = new List<DataListItem>();

        foreach (var culture in cultures)
        {
            try
            {
                var region = new RegionInfo(culture.LCID);
                items.Add(new DataListItem 
                { 
                    Name = region.EnglishName, 
                    Value = region.TwoLetterISORegionName 
                });
            }
            catch (ArgumentException)
            {
                // The culture does not have associated region, ignore it
            }
        }

        return items
            .GroupBy(i => i.Name)
            .Select(g => g.First())
            .OrderBy(i => i.Name)
            .ToList();
    }
}