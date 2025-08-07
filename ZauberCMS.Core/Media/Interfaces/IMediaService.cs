using ZauberCMS.Core.Media.Models;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Media.Interfaces;

public interface IMediaService
{
    Task<Media?> GetMediaAsync(GetMediaParameters parameters, CancellationToken cancellationToken = default);
    Task<HandlerResult<Media>> SaveMediaAsync(SaveMediaParameters parameters, CancellationToken cancellationToken = default);
    Task<HandlerResult<Media>> QueryMediaAsync(QueryMediaParameters parameters, CancellationToken cancellationToken = default);
    Task<HandlerResult<Media>> DeleteMediaAsync(DeleteMediaParameters parameters, CancellationToken cancellationToken = default);
    Task<bool> HasChildMediaAsync(HasChildMediaParameters parameters, CancellationToken cancellationToken = default);
    Task<List<string>> GetRestrictedMediaUrlsAsync(GetRestrictedMediaUrlsParameters parameters, CancellationToken cancellationToken = default);
}
