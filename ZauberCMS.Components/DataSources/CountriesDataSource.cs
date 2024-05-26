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
        return CultureInfo.GetCultures(CultureTypes.SpecificCultures)
            .Select(culture => new RegionInfo(culture.LCID))
            .Distinct()
            .OrderBy(ri => ri.EnglishName)
            .Select(countryCode => new DataListItem { Name = countryCode.EnglishName, Value = countryCode.Name })
            .ToList();
    }
}