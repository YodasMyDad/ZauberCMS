using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Content.Interfaces;

public interface IContentService
{
    Task<Content?> GetContentAsync(GetContentParameters parameters, CancellationToken cancellationToken = default);
    Task<HandlerResult<Content>> SaveContentAsync(SaveContentParameters parameters, CancellationToken cancellationToken = default);
    Task<HandlerResult<Content>> QueryContentAsync(QueryContentParameters parameters, CancellationToken cancellationToken = default);
    Task<HandlerResult<Content>> DeleteContentAsync(DeleteContentParameters parameters, CancellationToken cancellationToken = default);
    Task<HandlerResult<Content>> CopyContentAsync(CopyContentParameters parameters, CancellationToken cancellationToken = default);
    Task<Content?> GetContentFromRequestAsync(GetContentFromRequestParameters parameters, CancellationToken cancellationToken = default);
    Task<ContentType?> GetContentTypeAsync(GetContentTypeParameters parameters, CancellationToken cancellationToken = default);
    Task<HandlerResult<ContentType>> SaveContentTypeAsync(SaveContentTypeParameters parameters, CancellationToken cancellationToken = default);
    Task<HandlerResult<ContentType>> QueryContentTypesAsync(QueryContentTypesParameters parameters, CancellationToken cancellationToken = default);
    Task<HandlerResult<ContentType>> DeleteContentTypeAsync(DeleteContentTypeParameters parameters, CancellationToken cancellationToken = default);
    Task<Domain?> GetDomainAsync(GetDomainParameters parameters, CancellationToken cancellationToken = default);
    Task<HandlerResult<Domain>> SaveDomainAsync(SaveDomainParameters parameters, CancellationToken cancellationToken = default);
    Task<HandlerResult<Domain>> QueryDomainAsync(QueryDomainParameters parameters, CancellationToken cancellationToken = default);
    Task<HandlerResult<Domain>> DeleteDomainAsync(DeleteDomainParameters parameters, CancellationToken cancellationToken = default);
    Task<bool> AnyContentAsync(AnyContentParameters parameters, CancellationToken cancellationToken = default);
    Task<bool> HasChildContentAsync(HasChildContentParameters parameters, CancellationToken cancellationToken = default);
    Task<bool> HasChildContentTypeAsync(HasChildContentTypeParameters parameters, CancellationToken cancellationToken = default);
    Task<List<string>> GetContentLanguagesAsync(GetContentLanguagesParameters parameters, CancellationToken cancellationToken = default);
    Task<List<Domain>> GetCachedDomainsAsync(CachedDomainsParameters parameters, CancellationToken cancellationToken = default);
    Task<HandlerResult<Content>> ClearUnpublishedContentAsync(ClearUnpublishedContentParameters parameters, CancellationToken cancellationToken = default);
    Task<HandlerResult<Content>> GetDataGridContentAsync(DataGridContentParameters parameters, CancellationToken cancellationToken = default);
}
