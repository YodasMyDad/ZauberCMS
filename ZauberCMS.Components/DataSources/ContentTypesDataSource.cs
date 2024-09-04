using MediatR;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Content.Commands;
using ZauberCMS.Core.Content.Interfaces;
using ZauberCMS.Core.Content.Models;

namespace ZauberCMS.Components.DataSources;

public class ContentTypesDataSource(IMediator mediator) : IDataListSource
{
    public string Name => "Content Types";
    public string Description => "List of all content types";
    public string Icon => "content_copy";
    public string FullName => GetType().FullName ?? string.Empty;

    public IEnumerable<DataListItem> GetItems(IServiceScope scope, Content? currentContent)
    {
        var contentTypes = mediator.Send(new QueryContentTypesCommand
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