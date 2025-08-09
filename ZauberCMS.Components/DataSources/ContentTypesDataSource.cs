using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Content.Interfaces;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Content.Parameters;

namespace ZauberCMS.Components.DataSources;

public class ContentTypesDataSource(IContentService contentService) : IDataListSource
{
    public string Name => "Content Types";
    public string Description => "List of all content types";
    public string Icon => "content_copy";
    public string FullName => GetType().FullName ?? string.Empty;

    public IEnumerable<DataListItem> GetItems(IServiceScope scope, Content? currentContent)
    {
        var contentTypes = contentService.QueryContentTypesAsync(new QueryContentTypesParameters
        {
            OrderBy = GetContentTypesOrderBy.Name,
            AmountPerPage = 300,
            AsNoTracking = true
        }).GetAwaiter().GetResult();
        
        return contentTypes.Items.Select(x => new DataListItem
            {
                // Get the name of the component
                Name = x.Name!,
                // Get the full name of the component
                Value = x.Id.ToString(),
            })
            .OrderBy(x => x.Name)
            .ToList();
    }
}