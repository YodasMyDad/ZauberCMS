using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using ZauberCMS.Core.Content.Interfaces;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Localization;
using ZauberCMS.Core.Content.Parameters;
using ZauberCMS.Core.Data;

namespace ZauberCMS.Components.DataSources;

public class ContentTypesDataSource(IContentService contentService, IStringLocalizer<ZauberText> text) : IDataListSource
{
    public string Name => text["shared_contentTypesSource"];
    public string Description => text["shared_contentTypesSourceInfo"];
    public string Icon => "content_copy";
    public string FullName => GetType().FullName ?? string.Empty;

    /// <summary>
    /// The synchronous form, for older callers: a plain synchronous query on the scope's context, never a wait on
    /// the async one (that could deadlock a Blazor circuit, ZA-054).
    /// </summary>
    public IEnumerable<DataListItem> GetItems(IServiceScope scope, Content? currentContent) =>
        ToItems(scope.ServiceProvider.GetRequiredService<IZauberDbContext>().ContentTypes.AsNoTracking()
            .Where(x => !x.IsElementType && !x.IsComposition && !x.IsFolder)
            .OrderBy(x => x.Name)
            .Take(MaxItems)
            .ToList());

    public async Task<IEnumerable<DataListItem>> GetItemsAsync(IServiceScope scope, Content? currentContent,
        CancellationToken cancellationToken = default)
    {
        var contentTypes = await contentService.QueryContentTypesAsync(new QueryContentTypesParameters
        {
            OrderBy = GetContentTypesOrderBy.Name,
            AmountPerPage = MaxItems,
            AsNoTracking = true
        }, cancellationToken);
        return ToItems(contentTypes.Items);
    }

    private const int MaxItems = 300;

    private static List<DataListItem> ToItems(IEnumerable<ContentType> contentTypes) => contentTypes
        .Select(x => new DataListItem
        {
            // Get the name of the component
            Name = x.Name!,
            // Get the full name of the component
            Value = x.Id.ToString(),
        })
        .OrderBy(x => x.Name)
        .ToList();
}
