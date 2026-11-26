using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZauberCMS.Core.Content.Interfaces;
using ZauberCMS.Core.Content.Mapping;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Plugins;
using ZauberCMS.Core.Shared.Models;
using ZauberCMS.Core.Tags.Services;

namespace ZauberCMS.Core.Content.Services;

/// <summary>
/// Reads the blocks a content item owns (through the <see cref="INestedContentReferenceProvider"/> of each property's
/// editor) and walks a whole tree of them. One instance per operation: it caches content types for its lifetime.
/// </summary>
internal sealed class NestedContentTree(IZauberDbContext dbContext, ExtensionManager extensionManager, ILogger logger)
{
    /// <summary>Deepest block nesting that is walked. Guards against cycles in stored data and crafted values.</summary>
    public const int MaxDepth = 20;

    /// <summary>Most blocks one tree may hold.</summary>
    public const int MaxBlocks = 5000;

    private readonly Dictionary<Guid, Dictionary<Guid, NestedProperty>> _propertiesByContentType = new();
    private Dictionary<string, INestedContentReferenceProvider>? _providers;

    /// <summary>A property whose editor owns nested content.</summary>
    public sealed record NestedProperty(PropertyType Property, INestedContentReferenceProvider Provider);

    /// <summary>One block in a walked tree.</summary>
    /// <param name="Values">What the walk read the block's children from: the caller's draft, the stored draft
    /// or the stored row.</param>
    /// <param name="Stored">The stored row (not tracked; property data and draft included), or null for a new block.</param>
    public sealed record Node(Guid Id, Guid ParentId, Models.Content Values, Models.Content? Stored, bool FromCaller,
        int Depth);

    public sealed class Walk
    {
        /// <summary>Parents before their children.</summary>
        public List<Node> Nodes { get; } = [];
        public HashSet<Guid> Ids { get; } = [];

        /// <summary>The tree is deeper or larger than the limits; the rest was not walked.</summary>
        public bool Truncated { get; set; }
    }

    /// <summary>Discovered providers by editor alias. A site's own provider wins over the built-in one.</summary>
    public IReadOnlyDictionary<string, INestedContentReferenceProvider> Providers
    {
        get
        {
            if (_providers != null) return _providers;
            _providers = new Dictionary<string, INestedContentReferenceProvider>(StringComparer.OrdinalIgnoreCase);
            var coreAssembly = typeof(NestedContentTree).Assembly;
            foreach (var provider in extensionManager.GetInstances<INestedContentReferenceProvider>(true).Values
                         .OrderBy(p => p.GetType().Assembly == coreAssembly ? 0 : 1))
            {
                if (_providers.TryGetValue(provider.EditorAlias, out var existing) &&
                    existing.GetType().Assembly != coreAssembly)
                {
                    logger.LogWarning(
                        "Two nested content reference providers for the editor {EditorAlias}: {Used} and {Ignored}",
                        provider.EditorAlias, existing.GetType().FullName, provider.GetType().FullName);
                    continue;
                }

                _providers[provider.EditorAlias] = provider;
            }

            return _providers;
        }
    }

    /// <summary>The properties of the content's type (and its compositions) whose editor owns nested content.</summary>
    public async Task<IReadOnlyDictionary<Guid, NestedProperty>> GetNestedPropertiesAsync(Guid contentTypeId,
        CancellationToken cancellationToken)
    {
        if (_propertiesByContentType.TryGetValue(contentTypeId, out var cached)) return cached;

        var properties = new Dictionary<Guid, NestedProperty>();
        var contentType = await dbContext.ContentTypes.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == contentTypeId, cancellationToken);
        if (contentType != null)
        {
            var all = contentType.ContentProperties.ToList();
            if (contentType.CompositionIds.Count > 0)
            {
                var compositionIds = contentType.CompositionIds;
                var compositions = await dbContext.ContentTypes.AsNoTracking()
                    .Where(x => compositionIds.Contains(x.Id)).ToListAsync(cancellationToken);
                all.AddRange(compositions.SelectMany(x => x.ContentProperties));
            }

            foreach (var property in all)
            {
                if (property.ComponentAlias != null &&
                    Providers.TryGetValue(property.ComponentAlias, out var provider))
                {
                    properties.TryAdd(property.Id, new NestedProperty(property, provider));
                }
            }
        }

        _propertiesByContentType[contentTypeId] = properties;
        return properties;
    }

    /// <summary>The blocks the content points at, per property, in order.</summary>
    public async Task<List<(ContentPropertyValue Value, NestedProperty Property, IReadOnlyList<Guid> Ids)>>
        GetReferencesAsync(Models.Content content, CancellationToken cancellationToken)
    {
        var result = new List<(ContentPropertyValue, NestedProperty, IReadOnlyList<Guid>)>();
        var nestedProperties = await GetNestedPropertiesAsync(content.ContentTypeId, cancellationToken);
        if (nestedProperties.Count == 0) return result;

        foreach (var value in content.PropertyData)
        {
            if (nestedProperties.TryGetValue(value.ContentTypePropertyId, out var property))
            {
                result.Add((value, property, property.Provider.GetReferencedIds(value.Value)));
            }
        }

        return result;
    }

    /// <summary>
    /// Every block reachable from <paramref name="root"/>'s values, parents first. A block id the caller has a draft
    /// for is read from that draft; any other is read from the database, from its stored draft when
    /// <paramref name="useStoredDrafts"/> is set and it has one. Ids that are not blocks (missing, or a page) are
    /// skipped, and a block reached a second time is walked once.
    /// </summary>
    public async Task<Walk> WalkAsync(Models.Content root, IReadOnlyDictionary<Guid, Models.Content>? drafts,
        bool useStoredDrafts, CancellationToken cancellationToken)
    {
        var walk = new Walk();
        var seen = new HashSet<Guid> { root.Id };
        var level = new List<(Guid Id, Models.Content Values)> { (root.Id, root) };

        for (var depth = 0; level.Count > 0; depth++)
        {
            var references = new List<(Guid ParentId, Guid Id)>();
            foreach (var (parentId, values) in level)
            {
                foreach (var (_, _, ids) in await GetReferencesAsync(values, cancellationToken))
                {
                    foreach (var id in ids)
                    {
                        if (seen.Add(id))
                        {
                            references.Add((parentId, id));
                        }
                        else
                        {
                            logger.LogWarning("Block {BlockId} is referenced more than once in the tree of content {ContentId}",
                                id, root.Id);
                        }
                    }
                }
            }

            if (references.Count == 0) break;

            // Blocks at level MaxDepth are walked; only blocks below them are over the limit
            if (depth >= MaxDepth)
            {
                logger.LogWarning("Blocks of content {ContentId} are nested deeper than {MaxDepth}; the rest is ignored",
                    root.Id, MaxDepth);
                walk.Truncated = true;
                break;
            }

            var stored = await LoadAsync(references.Select(x => x.Id).ToList(), cancellationToken);
            level = [];
            foreach (var (parentId, id) in references)
            {
                stored.TryGetValue(id, out var row);
                Models.Content values;
                var fromCaller = false;
                if (drafts != null && drafts.TryGetValue(id, out var draft))
                {
                    values = draft;
                    fromCaller = true;
                }
                else if (row is { IsNestedContent: true })
                {
                    values = useStoredDrafts ? WithStoredDraft(row) : row;
                }
                else
                {
                    continue;
                }

                if (walk.Ids.Count >= MaxBlocks)
                {
                    logger.LogWarning("Content {ContentId} has more than {MaxBlocks} blocks; the rest is ignored",
                        root.Id, MaxBlocks);
                    walk.Truncated = true;
                    return walk;
                }

                walk.Nodes.Add(new Node(id, parentId, values, row, fromCaller, depth));
                walk.Ids.Add(id);
                level.Add((id, values));
            }
        }

        return walk;
    }

    /// <summary>Stored rows by id, not tracked, with property data and the stored draft.</summary>
    public async Task<Dictionary<Guid, Models.Content>> LoadAsync(List<Guid> ids, CancellationToken cancellationToken)
    {
        if (ids.Count == 0) return [];
        return await dbContext.Contents.AsNoTracking()
            .Include(x => x.PropertyData)
            .Include(x => x.UnpublishedContent)
            .AsSplitQuery()
            .Where(x => ids.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);
    }

    /// <summary>
    /// The row as the editor shows it: with its stored draft's values when it has one (a copy; the row is left as it is).
    /// </summary>
    public static Models.Content WithStoredDraft(Models.Content row)
    {
        if (row.UnpublishedContent == null) return row;
        var copy = row.MapToNew();
        copy.UnpublishedContent = row.UnpublishedContent;
        row.UnpublishedContent.JsonContent.MapBlockDraftTo(copy);
        return copy;
    }

    /// <summary>
    /// Stages the hard delete of the given blocks: their property values, stored drafts and rows. Only nested content
    /// is deleted. Returns the tracked rows that will be deleted.
    /// </summary>
    public async Task<List<Models.Content>> StageDeleteAsync(IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken)
    {
        if (ids.Count == 0) return [];
        var idList = ids.ToList();
        var rows = await dbContext.Contents.AsTracking()
            .Where(x => idList.Contains(x.Id) && x.IsNestedContent)
            .ToListAsync(cancellationToken);
        if (rows.Count == 0) return rows;

        var rowIds = rows.Select(x => x.Id).ToList();
        dbContext.ContentPropertyValues.RemoveRange(
            await dbContext.ContentPropertyValues.AsTracking().Where(x => rowIds.Contains(x.ContentId))
                .ToListAsync(cancellationToken));
        await ContentTagLinks.StageRemoveAsync(dbContext, rowIds, cancellationToken);

        var draftIds = rows.Where(x => x.UnpublishedContentId != null).Select(x => x.UnpublishedContentId!.Value).ToList();
        if (draftIds.Count > 0)
        {
            dbContext.UnpublishedContent.RemoveRange(
                await dbContext.UnpublishedContent.AsTracking().Where(x => draftIds.Contains(x.Id))
                    .ToListAsync(cancellationToken));
        }

        foreach (var row in rows)
        {
            row.UnpublishedContentId = null;
            dbContext.Contents.Remove(row);
        }

        return rows;
    }

    /// <summary>Stages the removal of the stored drafts of the given blocks (the live rows stay).</summary>
    public async Task StageDiscardDraftsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        if (ids.Count == 0) return;
        var idList = ids.ToList();
        var rows = await dbContext.Contents.AsTracking()
            .Where(x => idList.Contains(x.Id) && x.IsNestedContent && x.UnpublishedContentId != null)
            .ToListAsync(cancellationToken);
        if (rows.Count == 0) return;

        var draftIds = rows.Select(x => x.UnpublishedContentId!.Value).ToList();
        dbContext.UnpublishedContent.RemoveRange(
            await dbContext.UnpublishedContent.AsTracking().Where(x => draftIds.Contains(x.Id))
                .ToListAsync(cancellationToken));
        foreach (var row in rows)
        {
            row.UnpublishedContentId = null;
        }
    }
}
