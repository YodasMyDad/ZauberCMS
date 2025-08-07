using ZauberCMS.Core.Tags.Models;
using ZauberCMS.Core.Tags.Parameters;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Tags.Interfaces;

public interface ITagService
{
    Task<HandlerResult<Tag>> SaveTagAsync(SaveTagParameters parameters, CancellationToken cancellationToken = default);
    Task<PaginatedList<Tag>> QueryTagAsync(QueryTagParameters parameters, CancellationToken cancellationToken = default);
    Task<HandlerResult<Tag?>> DeleteTagAsync(DeleteTagParameters parameters, CancellationToken cancellationToken = default);
    Task<HandlerResult<TagItem>> SaveTagItemAsync(SaveTagItemParameters parameters, CancellationToken cancellationToken = default);
    Task<HandlerResult<TagItem?>> DeleteTagItemAsync(DeleteTagItemParameters parameters, CancellationToken cancellationToken = default);
}
