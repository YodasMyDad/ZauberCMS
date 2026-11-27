using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ZauberCMS.Core.Media.Parameters;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Media.Interfaces;

public interface IMediaService
{
    Task<Models.Media?> GetMediaAsync(GetMediaParameters parameters, CancellationToken cancellationToken = default);
    Task<HandlerResult<Models.Media>> SaveMediaAsync(SaveMediaParameters parameters, CancellationToken cancellationToken = default);
    Task<PaginatedList<Models.Media>> QueryMediaAsync(QueryMediaParameters parameters, CancellationToken cancellationToken = default);
    Task<HandlerResult<Models.Media>> DeleteMediaAsync(DeleteMediaParameters parameters, CancellationToken cancellationToken = default);

    /// <summary>Takes an item out of the recycle bin, with the items that went to the bin with it.</summary>
    Task<HandlerResult<Models.Media>> RestoreMediaAsync(RestoreMediaParameters parameters, CancellationToken cancellationToken = default);

    /// <summary>Permanently deletes everything in the media recycle bin; <c>Entity</c> is the number of items deleted.</summary>
    Task<HandlerResult<int?>> EmptyMediaRecycleBinAsync(EmptyMediaRecycleBinParameters parameters, CancellationToken cancellationToken = default);

    /// <summary>The pages (and blocks) and members whose property values point at the given media items.</summary>
    Task<List<Models.MediaUsage>> GetMediaUsageAsync(GetMediaUsageParameters parameters, CancellationToken cancellationToken = default);
    Task<bool> HasChildMediaAsync(HasChildMediaParameters parameters, CancellationToken cancellationToken = default);

    /// <summary>
    /// A page of an admin media tree node (a folder's children, or the top level), by name, with which folders have
    /// children (Roadmap D26). The live media, or the recycle bin with <c>InRecycleBin</c>.
    /// </summary>
    Task<TreeLevel<Models.Media>> GetMediaTreeLevelAsync(MediaTreeLevelParameters parameters, CancellationToken cancellationToken = default);
    Task<Dictionary<string, Guid>> GetRestrictedMediaUrlsAsync(GetRestrictedMediaUrlsParameters parameters, CancellationToken cancellationToken = default);
    Task<Dictionary<string, Models.RestrictedMediaEntry>> GetRestrictedMediaAccessAsync(GetRestrictedMediaUrlsParameters parameters, CancellationToken cancellationToken = default);
}