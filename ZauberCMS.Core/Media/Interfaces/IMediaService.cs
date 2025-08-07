using ZauberCMS.Core.Media.Models;
using ZauberCMS.Core.Media.Parameters;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Media.Interfaces;

public interface IMediaService
{
    Task<Media?> GetMediaAsync(GetMediaParameters parameters, CancellationToken cancellationToken = default);
    Task<HandlerResult<Media>> SaveMediaAsync(SaveMediaParameters parameters, CancellationToken cancellationToken = default);
    Task<PaginatedList<Media>> QueryMediaAsync(QueryMediaParameters parameters, CancellationToken cancellationToken = default);
    Task<HandlerResult<Media>> DeleteMediaAsync(DeleteMediaParameters parameters, CancellationToken cancellationToken = default);
    Task<bool> HasChildMediaAsync(HasChildMediaParameters parameters, CancellationToken cancellationToken = default);
    Task<Dictionary<string, Guid>> GetRestrictedMediaUrlsAsync(GetRestrictedMediaUrlsParameters parameters, CancellationToken cancellationToken = default);
}
