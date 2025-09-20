using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Content.Parameters;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Content.Interfaces;

public interface IContentVersioningService
{
    /// <summary>
    /// Creates a new version of content
    /// </summary>
    Task<HandlerResult<ContentVersion>> CreateVersionAsync(CreateContentVersionParameters parameters, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes a specific version
    /// </summary>
    Task<HandlerResult<ContentVersion>> PublishVersionAsync(PublishContentVersionParameters parameters, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets versions for a content item
    /// </summary>
    Task<PaginatedList<ContentVersion>> GetContentVersionsAsync(QueryContentVersionsParameters parameters, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific version
    /// </summary>
    Task<ContentVersion?> GetVersionAsync(GetContentVersionParameters parameters, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a version (with safety checks)
    /// </summary>
    Task<HandlerResult<bool>> DeleteVersionAsync(DeleteContentVersionParameters parameters, CancellationToken cancellationToken = default);

    /// <summary>
    /// Compares two versions and returns the differences
    /// </summary>
    Task<ContentVersionComparison> CompareVersionsAsync(CompareContentVersionsParameters parameters, CancellationToken cancellationToken = default);
}
