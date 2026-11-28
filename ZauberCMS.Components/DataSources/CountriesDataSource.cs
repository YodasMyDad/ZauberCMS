using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using ZauberCMS.Core.Content.Interfaces;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Localization;

namespace ZauberCMS.Components.DataSources;

public class CountriesDataSource(IStringLocalizer<ZauberText> text) : IDataListSource
{
    public string Name => text["shared_countriesSource"];
    public string Description => text["shared_countriesSourceInfo"];
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