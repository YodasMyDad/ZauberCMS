using System.Linq.Dynamic.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ZauberCMS.Core.Content.Interfaces;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Content.Parameters;
using ZauberCMS.Core.Content.Mapping;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Plugins;
using ZauberCMS.Core.Settings;
using ZauberCMS.Core.Shared.Models;
using ZauberCMS.Core.Tags.Services;
using ZauberCMS.Core.Membership.Interfaces;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Shared;
using System.Text.Json;

namespace ZauberCMS.Core.Content.Services;

// Content types: get, save, query and delete.
public partial class ContentService
{
    /// <summary>
    /// Retrieves a single content type by id.
    /// </summary>
    /// <param name="parameters">Parameters containing the content type id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The content type or null.</returns>
    public async Task<ContentType?> GetContentTypeAsync(GetContentTypeParameters parameters,
        CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        return await dbContext.ContentTypes.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == parameters.Id, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Creates or updates a content type and logs audit entries. Ensures alias uniqueness.
    /// </summary>
    /// <param name="parameters">The content type to save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result including success state and messages.</returns>
    public async Task<HandlerResult<ContentType>> SaveContentTypeAsync(SaveContentTypeParameters parameters,
        CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var user = await currentUserAccessor.GetUserAsync(cancellationToken);
        var handlerResult = new HandlerResult<ContentType>();
        var isUpdate = false;

        if (parameters.ContentType == null)
        {
            handlerResult.AddMessage("ContentType is null", ResultMessageType.Error);
            return handlerResult;
        }

        if (parameters.ContentType.Alias.IsNullOrWhiteSpace())
        {
            parameters.ContentType.Alias = parameters.ContentType.Name.ToAlias();
        }

        // Each page reads its values by alias: two properties with one alias (on the type, or through its
        // compositions) would make one value hide the other (ZA-028). Refused, as Umbraco does.
        var aliasClash = await FindPropertyAliasClashAsync(dbContext, parameters.ContentType, cancellationToken);
        if (aliasClash != null)
        {
            handlerResult.AddMessage(aliasClash, ResultMessageType.Error);
            return handlerResult;
        }

        var contentType = await dbContext.ContentTypes.FirstOrDefaultAsync(x => x.Id == parameters.ContentType.Id, cancellationToken);
        if (contentType == null)
        {
            var containsAlias = await dbContext.ContentTypes.AnyAsync(x => x.Alias == parameters.ContentType.Alias, cancellationToken);
            if (containsAlias)
            {
                handlerResult.AddMessage("Content Type Alias already exists, change the content type name",
                    ResultMessageType.Error);
                return handlerResult;
            }

            contentType = parameters.ContentType;
            contentType.LastUpdatedById = user?.Id;
            dbContext.ContentTypes.Add(contentType);
        }
        else
        {
            isUpdate = true;
            parameters.ContentType.MapTo(contentType);
            contentType.LastUpdatedById = user?.Id;
            contentType.DateUpdated = DateTime.UtcNow;
        }

        var actionText = isUpdate ? "Updated" : "Created";
        AddAuditIfUser(dbContext, user, contentType.Name, actionText);

        var savedType = await dbContext.SaveChangesAndLog(contentType, handlerResult, cacheService, extensionManager,
            cancellationToken);
        if (savedType.Success)
        {
            // Cached front-end pages hold their content type (IncludeChildren, the view)
            cacheService.ClearCachedItemsWithPrefix(nameof(Models.Content));
        }

        return savedType;
    }

    /// <summary>
    /// The error to show when <paramref name="contentType"/> as it is about to be saved would give a page two
    /// properties with one alias (ignoring case): on the type itself, between the type and its compositions, or, for a
    /// composition, on any type that uses it. Null when every alias is unique.
    /// </summary>
    private static async Task<string?> FindPropertyAliasClashAsync(IZauberDbContext dbContext, ContentType contentType,
        CancellationToken cancellationToken)
    {
        // Content types are few and their properties are a JSON column: compare in memory
        var others = await dbContext.ContentTypes.AsNoTracking()
            .Where(x => x.Id != contentType.Id)
            .ToListAsync(cancellationToken);
        var byId = others.ToDictionary(x => x.Id);
        byId[contentType.Id] = contentType;

        var typesToCheck = new List<ContentType> { contentType };
        if (contentType.IsComposition)
        {
            typesToCheck.AddRange(others.Where(x => x.CompositionIds.Contains(contentType.Id)));
        }

        foreach (var type in typesToCheck)
        {
            var sources = new List<ContentType> { type };
            sources.AddRange(type.CompositionIds.Distinct()
                .Where(id => id != type.Id)
                .Select(byId.GetValueOrDefault)
                .OfType<ContentType>());

            var seen = new Dictionary<string, (ContentType Source, Guid PropertyId)>(StringComparer.OrdinalIgnoreCase);
            foreach (var source in sources)
            {
                foreach (var property in source.ContentProperties.Where(p => !p.Alias.IsNullOrWhiteSpace()))
                {
                    var alias = property.Alias!;
                    if (seen.TryAdd(alias, (source, property.Id)))
                    {
                        continue;
                    }

                    var (first, firstPropertyId) = seen[alias];
                    if (firstPropertyId == property.Id)
                    {
                        // The same property reached twice (a copy of a composition's property): not a clash
                        continue;
                    }

                    if (!ReferenceEquals(first, contentType) && !ReferenceEquals(source, contentType))
                    {
                        // A clash this save doesn't touch (between two other types, already stored): not this save's
                        // to refuse, or a composition could never be saved again
                        continue;
                    }

                    return ReferenceEquals(first, source)
                        ? $"Two properties on '{source.Name}' use the alias '{alias}'. Give each property its own alias."
                        : $"The alias '{alias}' is used by '{first.Name}' and '{source.Name}', which are both on " +
                          $"'{type.Name}'. Give each property its own alias.";
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Queries content types with filtering and paging.
    /// </summary>
    /// <param name="parameters">Query options including filters and paging.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Paged list of content types.</returns>
    public async Task<PaginatedList<ContentType>> QueryContentTypesAsync(QueryContentTypesParameters parameters,
        CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();

        IQueryable<ContentType> query;

        // Start from external query if provided, otherwise from DbSet
        if (parameters.Query is not null)
        {
            query = parameters.Query.Invoke();
        }
        else
        {
            query = dbContext.ContentTypes.AsQueryable();

            if (parameters.AsNoTracking)
            {
                query = query.AsNoTracking();
            }

            if (parameters.Ids.Count != 0)
            {
                query = query.Where(x => parameters.Ids.Contains(x.Id));
            }

            if (!parameters.SearchTerm.IsNullOrWhiteSpace())
            {
                // LOWER() in SQL on the column, the term lowered here without the server's culture: a culture
                // such as Turkish lowers "I" to "ı" and the search then finds nothing (ZA-017)
                var searchTerm = parameters.SearchTerm.ToLowerInvariant();
#pragma warning disable CA1304, CA1311, CA1862 // EF translates ToLower() on the column to SQL LOWER(); the culture overloads are not translatable
                query = query.Where(x => x.Name != null && x.Name.ToLower().Contains(searchTerm));
#pragma warning restore CA1304, CA1311, CA1862
            }

            if (parameters.OnlyElementTypes)
            {
                query = query.Where(x => x.IsElementType == true);
            }
            else if (!parameters.IncludeElementTypes)
            {
                query = query.Where(x => x.IsElementType == false);
            }

            if (parameters.OnlyCompositions)
            {
                query = query.Where(x => x.IsComposition == true);
            }
            else if (!parameters.IncludeCompositions)
            {
                query = query.Where(x => x.IsComposition == false);
            }

            if (parameters.RootOnly)
            {
                query = query.Where(x => x.AllowAtRoot);
            }

            if (parameters.OnlyFolders)
            {
                query = query.Where(x => x.IsFolder == true);
            }
            else if (!parameters.IncludeFolders)
            {
                query = query.Where(x => x.IsFolder == false);
            }

            if (parameters.ParentId != null)
            {
                query = query.Where(x => x.ParentId == parameters.ParentId);
            }

            if (parameters.WhereClause != null)
            {
                query = query.Where(parameters.WhereClause);
            }

            query = parameters.OrderBy switch
            {
                GetContentTypesOrderBy.DateUpdated => query.OrderBy(p => p.DateUpdated),
                GetContentTypesOrderBy.DateUpdatedDescending => query.OrderByDescending(p => p.DateUpdated),
                GetContentTypesOrderBy.DateCreated => query.OrderBy(p => p.DateCreated),
                GetContentTypesOrderBy.DateCreatedDescending => query.OrderByDescending(p => p.DateCreated),
                GetContentTypesOrderBy.Name => query.OrderBy(p => p.Name),
                _ => query.OrderByDescending(p => p.DateUpdated)
            };
        }

        return await query.ToPaginatedListAsync(parameters.PageIndex, parameters.AmountPerPage, cancellationToken);
    }

    /// <summary>
    /// Deletes a content type if it is unused and has no children. Logs audit entries.
    /// </summary>
    /// <param name="parameters">Parameters containing the content type id to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result including success state and messages.</returns>
    public async Task<HandlerResult<ContentType>> DeleteContentTypeAsync(DeleteContentTypeParameters parameters,
        CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var user = await currentUserAccessor.GetUserAsync(cancellationToken);
        var handlerResult = new HandlerResult<ContentType>();

        // Check for regular content using this ContentType: drafts and recycled pages too, because deleting the
        // type cascades to every row that uses it (ZA-070)
        var contentUsingContentType = await dbContext.Contents.AsNoTracking()
            .AnyAsync(x => x.ContentTypeId == parameters.ContentTypeId && !x.IsNestedContent, cancellationToken);
        if (contentUsingContentType)
        {
            handlerResult.Success = false;
            handlerResult.AddMessage("Unable to delete, because this ContentType is being used by content items",
                ResultMessageType.Warning);
            return handlerResult;
        }

        // Check for nested content using this ContentType (Element Types).
        // A nested content row may still exist after its referencing property has been removed
        // from a ContentType (or its composition removed) — the property's JSON value lingers,
        // along with the nested Content row. Treat those as orphans and clean them up so the
        // Element Type can actually be deleted instead of being blocked forever.
        // Every nested row, not one page of published ones (ZA-070)
        var nestedContentUsingContentType = await dbContext.Contents.AsNoTracking()
            .Where(x => x.ContentTypeId == parameters.ContentTypeId && x.IsNestedContent)
            .ToListAsync(cancellationToken);
        if (nestedContentUsingContentType.Count != 0)
        {
            // Build the set of property type IDs that are still attached to some ContentType.
            // ContentProperties is stored as JSON inside ZauberContentTypes, so we materialise
            // the lists in memory and flatten.
            var validPropertyTypeIds = (await dbContext.ContentTypes.AsNoTracking()
                    .Select(ct => ct.ContentProperties)
                    .ToListAsync(cancellationToken))
                .SelectMany(props => props)
                .Select(p => p.Id)
                .ToHashSet();

            var liveItems = new List<Models.Content>();
            var orphanedItems = new List<Models.Content>();

            foreach (var nestedItem in nestedContentUsingContentType)
            {
                var idString = nestedItem.Id.ToString();
                var hasLiveReference = await dbContext.ContentPropertyValues
                    .AnyAsync(pv =>
                            validPropertyTypeIds.Contains(pv.ContentTypePropertyId) &&
                            pv.Value != null && pv.Value.Contains(idString),
                        cancellationToken);

                if (hasLiveReference)
                {
                    // One is enough to refuse; don't scan the property values once per remaining block
                    liveItems.Add(nestedItem);
                    break;
                }
                else
                {
                    orphanedItems.Add(nestedItem);
                }
            }

            if (liveItems.Count != 0)
            {
                handlerResult.Success = false;
                handlerResult.AddMessage(
                    "Unable to delete, because this Element Type is used by nested content",
                    ResultMessageType.Warning);
                return handlerResult;
            }

            // Only orphans remain — purge the dangling rows so the cascade-delete can proceed
            // (Content → ContentType is Cascade, but PropertyData → Content is NoAction, so we
            // have to remove property data and unpublished snapshots ourselves). Re-fetch from
            // the local dbContext because QueryContentAsync above runs in its own scope, so the
            // returned entities are detached relative to this dbContext.
            foreach (var orphanItem in orphanedItems)
            {
                var orphan = await dbContext.Contents.FirstOrDefaultAsync(x => x.Id == orphanItem.Id, cancellationToken);
                if (orphan == null) continue;

                var orphanPropertyValues = dbContext.ContentPropertyValues
                    .Where(pv => pv.ContentId == orphan.Id);
                dbContext.ContentPropertyValues.RemoveRange(orphanPropertyValues);

                if (orphan.UnpublishedContentId != null)
                {
                    var uContent = dbContext.UnpublishedContent
                        .FirstOrDefault(x => x.Id == orphan.UnpublishedContentId);
                    if (uContent != null) dbContext.UnpublishedContent.Remove(uContent);
                }

                dbContext.Contents.Remove(orphan);
            }

            // Their tag links go with them (TagItem has no foreign key to cascade)
            await ContentTagLinks.StageRemoveAsync(dbContext, orphanedItems.Select(x => x.Id).ToList(),
                cancellationToken);
        }

        var children =
            await QueryContentTypesAsync(new QueryContentTypesParameters { ParentId = parameters.ContentTypeId },
                cancellationToken);
        if (children.Items.Any())
        {
            handlerResult.Success = false;
            handlerResult.AddMessage("Unable to delete, because this ContentType has children",
                ResultMessageType.Warning);
            return handlerResult;
        }

        var contentType = await dbContext.ContentTypes.FirstOrDefaultAsync(x => x.Id == parameters.ContentTypeId, cancellationToken);
        if (contentType != null)
        {
            if (contentType.IsComposition)
            {
                var anyUsingThisComposition = await QueryContentTypesForCompositionAsync(dbContext, parameters.ContentTypeId, cancellationToken);
                if (anyUsingThisComposition.Items.Any())
                {
                    handlerResult.Success = false;
                    handlerResult.AddMessage(
                        "Unable to delete, because there are content types using this composition, remove it first",
                        ResultMessageType.Warning);
                    return handlerResult;
                }
            }

            // Orphan clean-up, audit row and delete commit together: a failed delete leaves no audit row
            // claiming it happened (ZA-070)
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                AddAuditIfUser(dbContext, user, contentType.Name, "Deleted");

                dbContext.ContentTypes.Remove(contentType);
                var deleteResult = await dbContext.SaveChangesAndLog(contentType, handlerResult, cacheService,
                    extensionManager, cancellationToken);
                if (deleteResult.Success)
                {
                    await transaction.CommitAsync(cancellationToken);
                    // Again after the commit: SaveChangesAndLog cleared before it, and a read in between cached the old rows
                    cacheService.ClearCachedItemsWithPrefix(nameof(Models.Content));
                }
                else
                {
                    await transaction.RollbackAsync(cancellationToken);
                }

                return deleteResult;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }

        handlerResult.AddMessage("Unable to delete, as no ContentType with that id exists", ResultMessageType.Warning);
        return handlerResult;
    }

    /// <summary>
    /// Checks whether a content type has child content types.
    /// </summary>
    /// <param name="parameters">Parent id and caching flag.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True when child content types exist.</returns>
    public async Task<bool> HasChildContentTypeAsync(HasChildContentTypeParameters parameters,
        CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        if (parameters.Cached)
        {
            var cacheKey = parameters.GenerateCacheKey<ContentType>("HasContentTypeChild");
            return await cacheService.GetSetCachedItemAsync(cacheKey,
                async () => await dbContext.ContentTypes.AsNoTracking().AnyAsync(c => c.ParentId == parameters.ParentId,
                    cancellationToken: cancellationToken));
        }

        return await dbContext.ContentTypes.AsNoTracking()
            .AnyAsync(c => c.ParentId == parameters.ParentId, cancellationToken: cancellationToken);
    }

    private static async Task<PaginatedList<ContentType>> QueryContentTypesForCompositionAsync(
        IZauberDbContext dbContext, Guid compositionId, CancellationToken cancellationToken)
    {
        var query = dbContext.ContentTypes.WhereHasCompositionsUsing(compositionId);
        return await query.ToPaginatedListAsync(1, int.MaxValue, cancellationToken);
    }
}
