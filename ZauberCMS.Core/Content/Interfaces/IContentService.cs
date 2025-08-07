using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Content.Parameters;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Content.Interfaces;

public interface IContentService
{
    Task<Content?> GetContentAsync(GetContentParameters parameters, CancellationToken cancellationToken = default);
    Task<HandlerResult<Content>> SaveContentAsync(SaveContentParameters parameters, CancellationToken cancellationToken = default);
    Task<PaginatedList<Content>> QueryContentAsync(QueryContentParameters parameters, CancellationToken cancellationToken = default);
    Task<HandlerResult<Content>> DeleteContentAsync(DeleteContentParameters parameters, CancellationToken cancellationToken = default);
    Task<HandlerResult<Content>> CopyContentAsync(CopyContentParameters parameters, CancellationToken cancellationToken = default);
    Task<EntryModel> GetContentFromRequestAsync(GetContentFromRequestParameters parameters, CancellationToken cancellationToken = default);
    Task<ContentType?> GetContentTypeAsync(GetContentTypeParameters parameters, CancellationToken cancellationToken = default);
    Task<HandlerResult<ContentType>> SaveContentTypeAsync(SaveContentTypeParameters parameters, CancellationToken cancellationToken = default);
    Task<PaginatedList<ContentType>> QueryContentTypesAsync(QueryContentTypesParameters parameters, CancellationToken cancellationToken = default);
    Task<HandlerResult<ContentType>> DeleteContentTypeAsync(DeleteContentTypeParameters parameters, CancellationToken cancellationToken = default);
    Task<Domain?> GetDomainAsync(GetDomainParameters parameters, CancellationToken cancellationToken = default);
    Task<HandlerResult<Domain>> SaveDomainAsync(SaveDomainParameters parameters, CancellationToken cancellationToken = default);
    Task<PaginatedList<Domain>> QueryDomainAsync(QueryDomainParameters parameters, CancellationToken cancellationToken = default);
    Task<HandlerResult<Domain>> DeleteDomainAsync(DeleteDomainParameters parameters, CancellationToken cancellationToken = default);
    Task<bool> AnyContentAsync(AnyContentParameters parameters, CancellationToken cancellationToken = default);
    Task<bool> HasChildContentAsync(HasChildContentParameters parameters, CancellationToken cancellationToken = default);
    Task<bool> HasChildContentTypeAsync(HasChildContentTypeParameters parameters, CancellationToken cancellationToken = default);
    Task<Dictionary<object, string>> GetContentLanguagesAsync(GetContentLanguagesParameters parameters, CancellationToken cancellationToken = default);
    Task<List<Domain>> GetCachedDomainsAsync(CachedDomainsParameters parameters, CancellationToken cancellationToken = default);
    Task<HandlerResult<Content>> ClearUnpublishedContentAsync(ClearUnpublishedContentParameters parameters, CancellationToken cancellationToken = default);
    Task<DataGridResult<Content>> GetDataGridContentAsync(DataGridContentParameters parameters, CancellationToken cancellationToken = default);
}
