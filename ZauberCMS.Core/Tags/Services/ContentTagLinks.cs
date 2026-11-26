using Microsoft.EntityFrameworkCore;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Plugins;
using ZauberCMS.Core.Tags.Interfaces;
using ZauberCMS.Core.Tags.Models;

namespace ZauberCMS.Core.Tags.Services;

/// <summary>
/// Stages an item's tag links (<see cref="TagItem"/> rows) from the values a save writes, through the
/// <see cref="ITagReferenceProvider"/> of each property's editor, on the caller's context: the save that writes the
/// values writes the links, in its transaction, with its save hooks (ZA-099, ZA-018). An item whose type has no tag
/// property keeps the links it has (code may have set them through <c>ITagService.SaveTagItemAsync</c>).
/// </summary>
internal static class ContentTagLinks
{
    /// <summary>Stages the links of <paramref name="items"/> to match their property values.</summary>
    public static async Task StageAsync(IZauberDbContext dbContext, ExtensionManager extensionManager,
        IReadOnlyCollection<Content.Models.Content> items, CancellationToken cancellationToken)
    {
        if (items.Count == 0) return;
        var providers = Providers(extensionManager);
        if (providers.Count == 0) return;

        var tagPropertiesByType = new Dictionary<Guid, Dictionary<Guid, ITagReferenceProvider>>();
        var wanted = new Dictionary<Guid, HashSet<Guid>>();
        foreach (var item in items)
        {
            if (!tagPropertiesByType.TryGetValue(item.ContentTypeId, out var tagProperties))
            {
                tagProperties = await TagPropertiesAsync(dbContext, item.ContentTypeId, providers, cancellationToken);
                tagPropertiesByType[item.ContentTypeId] = tagProperties;
            }

            if (tagProperties.Count == 0) continue;
            var tagIds = new HashSet<Guid>();
            foreach (var value in item.PropertyData)
            {
                // A value the save removes is still in the list until it is saved
                if (dbContext.Entry(value).State == EntityState.Deleted) continue;
                if (tagProperties.TryGetValue(value.ContentTypePropertyId, out var provider))
                {
                    tagIds.UnionWith(provider.GetTagIds(value.Value));
                }
            }

            wanted[item.Id] = tagIds;
        }

        if (wanted.Count == 0) return;

        // A value may still name a tag that was deleted since; a link to it would break the foreign key
        var named = wanted.Values.SelectMany(x => x).Distinct().ToList();
        var existingTags = named.Count == 0
            ? []
            : (await dbContext.Tags.AsNoTracking().Where(t => named.Contains(t.Id)).Select(t => t.Id)
                .ToListAsync(cancellationToken)).ToHashSet();

        var itemIds = wanted.Keys.ToList();
        var links = await dbContext.TagItems.AsTracking().Where(x => itemIds.Contains(x.ItemId))
            .ToListAsync(cancellationToken);
        foreach (var (itemId, tagIds) in wanted)
        {
            var current = links.Where(x => x.ItemId == itemId).ToList();
            dbContext.TagItems.RemoveRange(current.Where(x => !tagIds.Contains(x.TagId)));
            foreach (var tagId in tagIds.Where(existingTags.Contains).Except(current.Select(x => x.TagId)))
            {
                dbContext.TagItems.Add(new TagItem { TagId = tagId, ItemId = itemId });
            }
        }
    }

    /// <summary>Stages the removal of every link of the given items (they are being deleted).</summary>
    public static async Task StageRemoveAsync(IZauberDbContext dbContext, IReadOnlyCollection<Guid> itemIds,
        CancellationToken cancellationToken)
    {
        if (itemIds.Count == 0) return;
        foreach (var chunk in itemIds.Chunk(500))
        {
            dbContext.TagItems.RemoveRange(await dbContext.TagItems.AsTracking()
                .Where(x => chunk.Contains(x.ItemId)).ToListAsync(cancellationToken));
        }
    }

    /// <summary>Discovered providers by editor alias; a site's own provider wins over the built-in one.</summary>
    private static Dictionary<string, ITagReferenceProvider> Providers(ExtensionManager extensionManager)
    {
        var coreAssembly = typeof(ContentTagLinks).Assembly;
        var providers = new Dictionary<string, ITagReferenceProvider>(StringComparer.OrdinalIgnoreCase);
        foreach (var provider in extensionManager.GetInstances<ITagReferenceProvider>(true).Values
                     .OrderBy(p => p.GetType().Assembly == coreAssembly ? 0 : 1))
        {
            providers[provider.EditorAlias] = provider;
        }

        return providers;
    }

    /// <summary>The properties of the type (and its compositions) whose editor holds tags.</summary>
    private static async Task<Dictionary<Guid, ITagReferenceProvider>> TagPropertiesAsync(IZauberDbContext dbContext,
        Guid contentTypeId, Dictionary<string, ITagReferenceProvider> providers, CancellationToken cancellationToken)
    {
        var result = new Dictionary<Guid, ITagReferenceProvider>();
        var contentType = await dbContext.ContentTypes.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == contentTypeId, cancellationToken);
        if (contentType == null) return result;

        var properties = contentType.ContentProperties.ToList();
        if (contentType.CompositionIds.Count > 0)
        {
            var compositionIds = contentType.CompositionIds;
            properties.AddRange((await dbContext.ContentTypes.AsNoTracking()
                    .Where(x => compositionIds.Contains(x.Id)).ToListAsync(cancellationToken))
                .SelectMany(x => x.ContentProperties));
        }

        foreach (var property in properties)
        {
            if (property.ComponentAlias != null && providers.TryGetValue(property.ComponentAlias, out var provider))
            {
                result.TryAdd(property.Id, provider);
            }
        }

        return result;
    }
}
