using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Content.Parameters;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Content.Interfaces;

public interface IContentService
{
    Task<Models.Content?> GetContentAsync(GetContentParameters parameters, CancellationToken cancellationToken = default);
    Task<HandlerResult<Models.Content>> SaveContentAsync(SaveContentParameters parameters, CancellationToken cancellationToken = default);
    Task<PaginatedList<Models.Content>> QueryContentAsync(QueryContentParameters parameters, CancellationToken cancellationToken = default);
    Task<HandlerResult<Models.Content>> DeleteContentAsync(DeleteContentParameters parameters, CancellationToken cancellationToken = default);
    Task<HandlerResult<Models.Content>> CopyContentAsync(CopyContentParameters parameters, CancellationToken cancellationToken = default);
    Task<EntryModel> GetContentFromRequestAsync(GetContentFromRequestParameters parameters, CancellationToken cancellationToken = default);

    Task<ContentType?> GetContentTypeAsync(GetContentTypeParameters parameters, CancellationToken cancellationToken = default);
    Task<HandlerResult<ContentType>> SaveContentTypeAsync(SaveContentTypeParameters parameters, CancellationToken cancellationToken = default);
    Task<PaginatedList<ContentType>> QueryContentTypesAsync(QueryContentTypesParameters parameters, CancellationToken cancellationToken = default);
    Task<HandlerResult<ContentType>> DeleteContentTypeAsync(DeleteContentTypeParameters parameters, CancellationToken cancellationToken = default);
    Task<string?> ExportContentTypeAsync(string alias, bool includeContent = false);
    Task<HandlerResult<ContentType>> ImportContentTypeAsync(string json);

    Task<Domain> GetDomainAsync(GetDomainParameters parameters, CancellationToken cancellationToken = default);
    Task<HandlerResult<Domain>> SaveDomainAsync(SaveDomainParameters parameters, CancellationToken cancellationToken = default);
    Task<PaginatedList<Domain>> QueryDomainAsync(QueryDomainParameters parameters, CancellationToken cancellationToken = default);
    Task<HandlerResult<Domain?>> DeleteDomainAsync(DeleteDomainParameters parameters, CancellationToken cancellationToken = default);

    Task<bool> AnyContentAsync(CancellationToken cancellationToken = default);
    Task<bool> HasChildContentAsync(HasChildContentParameters parameters, CancellationToken cancellationToken = default);
    Task<bool> HasChildContentTypeAsync(HasChildContentTypeParameters parameters, CancellationToken cancellationToken = default);
    Task<Dictionary<object, string>> GetContentLanguagesAsync(GetContentLanguagesParameters parameters, CancellationToken cancellationToken = default);
    Task<List<Domain>> GetCachedDomainsAsync(CachedDomainsParameters parameters, CancellationToken cancellationToken = default);
    Task<HandlerResult<UnpublishedContent>> ClearUnpublishedContentAsync(ClearUnpublishedContentParameters parameters, CancellationToken cancellationToken = default);
    Task<DataGridResult<Models.Content>> GetDataGridContentAsync(DataGridContentParameters parameters, CancellationToken cancellationToken = default);
    Task<HandlerResult<int>> CleanupOrphanedRelatedContentAsync(CleanupOrphanedRelatedContentParameters parameters, CancellationToken cancellationToken = default);
}