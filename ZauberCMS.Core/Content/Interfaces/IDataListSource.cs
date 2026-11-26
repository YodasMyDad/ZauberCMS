using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Content.Models;

namespace ZauberCMS.Core.Content.Interfaces;

public interface IDataListSource
{
    /// <summary>
    /// Name of the data list source
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Description of the data list source.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Represents the Icon property of a data list source.
    /// </summary>
    string Icon { get; }

    /// <summary>
    /// The full name of the implemented data source class including namespace.
    /// </summary>
    string FullName { get; }

    /// <summary>
    /// Retrieves a list of items from a data source.
    /// </summary>
    /// <param name="scope">The service scope used for dependency injection.</param>
    /// <param name="currentContent">The current content object.</param>
    /// <returns>A collection of DataListItem objects.</returns>
    IEnumerable<DataListItem> GetItems(IServiceScope scope, Models.Content? currentContent);

    /// <summary>
    /// Retrieves the items without blocking: what the list editors call. The default calls <see cref="GetItems"/>;
    /// a source that queries a database or a web service implements this instead (and can make
    /// <see cref="GetItems"/> do the same work synchronously).
    /// </summary>
    Task<IEnumerable<DataListItem>> GetItemsAsync(IServiceScope scope, Models.Content? currentContent,
        CancellationToken cancellationToken = default) => Task.FromResult(GetItems(scope, currentContent));
}