using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Content.Interfaces;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Plugins;

namespace ZauberCMS.Components.DataSources;

public class ContentViewsDataSource(ExtensionManager extensionManager) : IDataListSource
{
    public string Name => "Content Views";
    public string Description => "List of the available content views";
    public string Icon => "tag";
    public string FullName => GetType().FullName ?? string.Empty;

    public IEnumerable<DataListItem> GetItems(IServiceScope scope, Content? currentContent)
    {
        var allContentViews = extensionManager.GetInstances<IContentView>(true);
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