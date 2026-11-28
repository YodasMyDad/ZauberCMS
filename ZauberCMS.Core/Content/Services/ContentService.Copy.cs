using System.Linq.Dynamic.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
using System.Text.Json;

namespace ZauberCMS.Core.Content.Services;

// Copying a page (optionally with its subtree) and its blocks.
public partial class ContentService
{
    /// <summary>
    /// Gives each of <paramref name="copiedItems"/> copies of its blocks: each block its nested content properties
    /// point at is cloned with a new id (and its own blocks with it), and the property values are rewritten to the
    /// clones. Level by level, one query per level (ZA-073). <paramref name="originalToCloneMap"/> is shared by the
    /// whole copy, so a block reached twice gets one clone.
    /// </summary>
    private async Task CloneBlockListContentTreeAsync(IReadOnlyList<Models.Content> copiedItems, NestedContentTree tree,
        IZauberDbContext dbContext, User? currentUser, Dictionary<Guid, Guid> originalToCloneMap,
        CancellationToken cancellationToken)
    {
        var level = copiedItems.ToList();
        for (var depth = 0; level.Count > 0; depth++)
        {
            if (depth >= NestedContentTree.MaxDepth)
            {
                logger.LogWarning("Blocks of copied content are nested deeper than {MaxDepth}; the rest is not copied",
                    NestedContentTree.MaxDepth);
                return;
            }

            var references = new List<(Models.Content Owner, ContentPropertyValue Value,
                NestedContentTree.NestedProperty Property, IReadOnlyList<Guid> Ids)>();
            foreach (var item in level)
            {
                foreach (var (value, property, ids) in await tree.GetReferencesAsync(item, cancellationToken))
                {
                    references.Add((item, value, property, ids));
                }
            }

            var originals = await tree.LoadAsync(references.SelectMany(r => r.Ids)
                .Where(id => !originalToCloneMap.ContainsKey(id)).Distinct().ToList(), cancellationToken);
            var next = new List<Models.Content>();
            foreach (var (owner, value, property, ids) in references)
            {
                foreach (var origId in ids)
                {
                    // Already cloned (another property, another copied item), or not a block: the reference stays
                    if (originalToCloneMap.ContainsKey(origId) ||
                        !originals.TryGetValue(origId, out var original) || !original.IsNestedContent)
                    {
                        continue;
                    }

                    var clone = original.MapToNew();
                    clone.Id = Guid.NewGuid().NewSequentialGuid();
                    clone.DateCreated = DateTime.UtcNow;
                    clone.DateUpdated = DateTime.UtcNow;
                    clone.LastUpdatedById = currentUser?.Id;
                    clone.Deleted = false;
                    clone.RelatedContentId = owner.Id;
                    // Not the original block's draft (ZA-049), and blocks have no URL (ZA-067)
                    clone.UnpublishedContentId = null;
                    clone.Url = null;
                    clone.PropertyData = original.PropertyData.Select(p => new ContentPropertyValue
                    {
                        Id = Guid.NewGuid().NewSequentialGuid(),
                        DateCreated = p.DateCreated,
                        DateUpdated = p.DateUpdated,
                        ContentTypePropertyId = p.ContentTypePropertyId,
                        Value = p.Value,
                        ContentId = clone.Id,
                        Alias = p.Alias
                    }).ToList();

                    // Mapped before the next level, so a cycle in stored data stops at this clone
                    originalToCloneMap[origId] = clone.Id;
                    dbContext.Contents.Add(clone);
                    next.Add(clone);
                }

                value.Value = property.Provider.RemapIds(value.Value, originalToCloneMap) ?? value.Value;
            }

            level = next;
        }
    }

    /// <summary>
    /// Creates a copy of a content item and optionally all descendants. Copies property data and updates paths.
    /// </summary>
    /// <param name="parameters">Copy options including source, destination parent and whether to include descendants.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result including success state and messages.</returns>
    public async Task<HandlerResult<Models.Content>> CopyContentAsync(CopyContentParameters parameters,
        CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var user = await currentUserAccessor.GetUserAsync(cancellationToken);
        var handlerResult = new HandlerResult<Models.Content>();

        var contentToCopy = await dbContext.Contents
            .AsNoTracking()
            .Include(content => content.PropertyData)
            .FirstOrDefaultAsync(x => x.Id == parameters.ContentToCopy, cancellationToken);

        if (contentToCopy == null)
        {
            handlerResult.Success = false;
            handlerResult.AddMessage(text["content_copyNotFound"], ResultMessageType.Error);
            return handlerResult;
        }

        var idMap = new Dictionary<Guid, Guid>();
        // URLs handed out in this copy but not saved yet, so two copies never get the same one (ZA-046)
        var reservedUrls = new HashSet<string>(StringComparer.Ordinal);
        var newParentId = parameters.CopyTo ?? contentToCopy.ParentId;
        var copiedContent = await CreateCopyAsync(contentToCopy, user, newParentId);

        idMap[contentToCopy.Id] = copiedContent.Id;

        if (newParentId.HasValue)
        {
            var parentContent = await dbContext.Contents.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == newParentId, cancellationToken);
            if (parentContent != null)
            {
                copiedContent.Path = [.. parentContent.Path, copiedContent.Id];
            }
        }
        else
        {
            copiedContent.Path = [copiedContent.Id];
        }

        dbContext.Add(copiedContent);

        if (parameters.IncludeDescendants)
        {
            // Walk the tree by ParentId (nearest first, so a parent's copy exists before its children's) rather
            // than matching stored paths with raw SQL. Items in the recycle bin are not copied.
            var copiedPaths = new Dictionary<Guid, List<Guid>> { [copiedContent.Id] = copiedContent.Path };
            // Ids only for the walk; the rows are loaded once below, with their values (ZA-145)
            var descendantIds = await GetDescendantIdsAsync(dbContext, contentToCopy.Id, cancellationToken);
            var treeOrder = descendantIds.Select((id, index) => (id, index)).ToDictionary(x => x.id, x => x.index);
            var descendants = (await dbContext.Contents
                    .AsNoTracking()
                    .Include(content => content.PropertyData)
                    .Where(x => descendantIds.Contains(x.Id))
                    .ToListAsync(cancellationToken))
                .OrderBy(x => treeOrder[x.Id]);

            foreach (var descendant in descendants)
            {
                if (descendant.ParentId == null || descendant.Deleted ||
                    !idMap.TryGetValue(descendant.ParentId.Value, out var newParentIdForDescendant))
                {
                    continue;
                }

                var copiedDescendant = await CreateCopyAsync(descendant, user, newParentIdForDescendant);
                copiedDescendant.Path = [.. copiedPaths[newParentIdForDescendant], copiedDescendant.Id];
                copiedPaths[copiedDescendant.Id] = copiedDescendant.Path;
                idMap[descendant.Id] = copiedDescendant.Id;
                dbContext.Add(copiedDescendant);
            }
        }

        // Deep-copy nested block content referenced by BlockListEditor properties.
        // Without this the copy shares nested rows with the original — editing one mutates the other.
        // Use a single Original→Clone mapping shared across the whole copy operation so a block
        // referenced by multiple properties or multiple descendants resolves to the same new clone.
        var nestedIdMap = new Dictionary<Guid, Guid>();
        var cloneTree = new NestedContentTree(dbContext, extensionManager, logger);
        // The copied page and its copied descendants (tracked: they were added above), taken before any clone is
        // added to the change tracker
        var copiedPages = idMap.Values
            .Select(id => dbContext.Contents.Local.FirstOrDefault(c => c.Id == id))
            .OfType<Models.Content>()
            .ToList();
        await CloneBlockListContentTreeAsync(copiedPages, cloneTree, dbContext, user, nestedIdMap, cancellationToken);
        // The copies' tag links, from the copied values (ZA-099)
        await ContentTagLinks.StageAsync(dbContext, extensionManager,
            dbContext.Contents.Local.Where(c => dbContext.Entry(c).State == EntityState.Added).ToList(),
            cancellationToken);
        AddAuditIfUser(dbContext, user, contentToCopy.Name, "Copied");

        // One save for the copies, their blocks and the audit row; the save hooks see every row as added
        await using (var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken))
        {
            var saved = await dbContext.SaveChangesAndLog([copiedContent], copiedContent, handlerResult, cacheService,
                extensionManager, cancellationToken);
            if (!saved.Success)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return saved;
            }

            await transaction.CommitAsync(cancellationToken);
            cacheService.ClearCachedItemsWithPrefix(nameof(Models.Content));
        }

        handlerResult.AddMessage(text["content_copied"], ResultMessageType.Success);
        return handlerResult;

        async Task<Models.Content> CreateCopyAsync(Models.Content original, User? currentUser, Guid? parentId = null)
        {
            var copy = original.MapToNew();
            copy.Id = Guid.NewGuid().NewSequentialGuid();
            copy.Name = original.Name + " (Copy)";
            copy.Url = original.IsNestedContent
                ? null
                : await GenerateUniqueUrlAsync(dbContext,
                    (original.Url.IsNullOrWhiteSpace() ? new SlugHelper().GenerateSlug(original.Name) : original.Url) + "-copy",
                    cancellationToken, reserved: reservedUrls);
            // The original's pending draft belongs to the original; sharing the row breaks both on publish (ZA-049)
            copy.UnpublishedContentId = null;
            copy.LastUpdatedById = currentUser?.Id;
            copy.ParentId = parentId;
            copy.DateCreated = DateTime.UtcNow;
            copy.DateUpdated = DateTime.UtcNow;
            copy.Path = [];
            copy.Published = false;
            copy.Deleted = false;
            copy.PropertyData = original.PropertyData.Select(p => new ContentPropertyValue
            {
                Id = Guid.NewGuid().NewSequentialGuid(),
                DateUpdated = p.DateUpdated,
                DateCreated = p.DateCreated,
                ContentTypePropertyId = p.ContentTypePropertyId,
                Value = p.Value,
                ContentId = p.ContentId,
                Alias = p.Alias
            }).ToList();
            return copy;
        }
    }
}
