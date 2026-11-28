using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using ZauberCMS.Core.Content.Interfaces;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Localization;
using ZauberCMS.Core.Plugins;

namespace ZauberCMS.Components.DataSources;

public class ContentViewsDataSource(ExtensionManager extensionManager, IStringLocalizer<ZauberText> text) : IDataListSource
{
    public string Name => text["shared_contentViewsSource"];
    public string Description => text["shared_contentViewsSourceInfo"];
    public string Icon => "tag";
    public string FullName => GetType().FullName ?? string.Empty;

    public IEnumerable<DataListItem> GetItems(IServiceScope scope, Content? currentContent)
    {
        var localContentViews = extensionManager.GetInstances<IContentView>(true);
        var allContentViews = new Dictionary<string, IContentView>(localContentViews);
        return allContentViews.Values.Select(x =>
        {
            var type = x.GetType();
            return new DataListItem
            {
                // Get the name of the component
                Name = type.Name,
                // Get the full name of the component
                Value = type.FullName!
            };
        })
        .OrderBy(x => x.Name)
        .ToList();
    }
}