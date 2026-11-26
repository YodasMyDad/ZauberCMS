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
    /// <summary>
    /// Creates or updates a content item. A save of its values saves the item and its blocks as one change: one
    /// transaction, one audit entry, one version taken after the blocks are written; blocks the new values no longer
    /// reach are deleted with everything below them (<see cref="SaveContentParameters.Blocks"/>). With
    /// <see cref="SaveContentParameters.SaveUnpublishedOnly"/> the item and its edited blocks get drafts and the live
    /// site does not change; without it, pending drafts of the item's blocks are published too. A save with
    /// <see cref="SaveContentParameters.ExcludePropertyData"/> (sort, move, restore, …) changes only where or how
    /// the item is shown. Does not raise <c>AppState</c> events; the caller does, once, for the item.
    /// </summary>
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

    /// <summary>
    /// Public access: sets the roles that may see a page on the page and every page below it, in one transaction
    /// (one audit row; the <c>ContentRole</c> save hooks run, no content versions are made). Refused when an
    /// ancestor already restricts access. An empty list removes the restriction.
    /// </summary>
    Task<HandlerResult<Models.Content>> SetContentRolesAsync(SetContentRolesParameters parameters, CancellationToken cancellationToken = default);
    Task<DataGridResult<Models.Content>> GetDataGridContentAsync(DataGridContentParameters parameters, CancellationToken cancellationToken = default);
    Task<HandlerResult<int>> CleanupOrphanedRelatedContentAsync(CleanupOrphanedRelatedContentParameters parameters, CancellationToken cancellationToken = default);
}