using System.Linq.Dynamic.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ZauberCMS.Core.Content.Interfaces;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Content.Parameters;
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

// Exporting a content type (optionally with its content) as JSON, and importing it.
public partial class ContentService
{
    public async Task<string?> ExportContentTypeAsync(string alias, bool includeContent = false,
        CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();

        // Tabs and properties are columns of the type (JSON), not navigations: an Include of them threw
        var contentType = await dbContext.ContentTypes.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Alias == alias, cancellationToken);

        if (contentType == null) return null;

        var exportType = new ContentTypeExport
        {
            Name = contentType.Name ?? string.Empty,
            Description = contentType.Description ?? string.Empty,
            Alias = contentType.Alias ?? string.Empty,
            Icon = contentType.Icon ?? "description",
            IsElementType = contentType.IsElementType,
            AllowAtRoot = contentType.AllowAtRoot,
            EnableListView = contentType.EnableListView,
            IncludeChildren = contentType.IncludeChildren,
            AvailableContentViews = contentType.AvailableContentViews,
            Tabs = contentType.Tabs,
            ContentProperties = contentType.ContentProperties,
            IsFolder = contentType.IsFolder,
            IsComposition = contentType.IsComposition,
            MediaIdAsString = contentType.MediaIdAsString ?? string.Empty
        };

        // Map CompositionIds to aliases
        if (contentType.CompositionIds.Count != 0)
        {
            exportType.CompositionAliases = await dbContext.ContentTypes
                .Where(ct => contentType.CompositionIds.Contains(ct.Id))
                .Select(ct => ct.Alias!)
                .ToListAsync(cancellationToken);
        }

        // Map AllowedChildContentTypes to aliases
        if (contentType.AllowedChildContentTypes.Count != 0)
        {
            exportType.AllowedChildContentTypeAliases = await dbContext.ContentTypes
                .Where(ct => contentType.AllowedChildContentTypes.Contains(ct.Id))
                .Select(ct => ct.Alias!)
                .ToListAsync(cancellationToken);
        }

        // Map ParentId to alias
        if (contentType.ParentId.HasValue)
        {
            var parent = await dbContext.ContentTypes.FindAsync([contentType.ParentId.Value], cancellationToken);
            exportType.ParentAlias = parent?.Alias ?? string.Empty;
        }

        var package = new ContentTypePackage { Type = exportType };

        if (includeContent)
        {
            var rootContents = await dbContext.Contents
                .Where(c => c.ContentTypeId == contentType.Id && c.ParentId == null)
                .Include(c => c.PropertyData)
                .ToListAsync(cancellationToken);

            package.RootContents = await BuildContentExportTree(rootContents, dbContext, contentType, cancellationToken);
        }

        return JsonSerializer.Serialize(package, ExportJsonOptions);
    }

    private static readonly JsonSerializerOptions ExportJsonOptions = new() { WriteIndented = true };

    private static async Task<List<ContentExport>> BuildContentExportTree(List<Models.Content> contents,
        IZauberDbContext dbContext, ContentType contentType, CancellationToken cancellationToken)
    {
        var exports = new List<ContentExport>();

        foreach (var content in contents)
        {
            var exp = new ContentExport
            {
                Name = content.Name ?? string.Empty,
                Url = content.Url ?? string.Empty,
                ContentTypeAlias = content.ContentTypeAlias ?? string.Empty,
                Published = content.Published,
                Deleted = content.Deleted,
                HideFromNavigation = content.HideFromNavigation,
                InternalRedirectIdAsString = content.InternalRedirectIdAsString ?? string.Empty,
                SortOrder = content.SortOrder,
                ViewComponent = content.ViewComponent,
                LanguageIsoCode = content.Language?.LanguageIsoCode ?? string.Empty,
                // PropertyData
                PropertyData = new Dictionary<string, string>()
            };

            foreach (var pd in content.PropertyData)
            {
                var propType = contentType.ContentProperties.FirstOrDefault(p => p.Id == pd.ContentTypePropertyId);
                if (propType != null)
                {
                    exp.PropertyData[propType.Alias ?? propType.Name ?? string.Empty] = pd.Value;
                }
            }

            // Children recursively
            var children = await dbContext.Contents
                .Where(c => c.ParentId == content.Id)
                .Include(c => c.PropertyData)
                .ToListAsync(cancellationToken);
            exp.Children = await BuildContentExportTree(children, dbContext, contentType, cancellationToken);

            exports.Add(exp);
        }

        return exports;
    }

    public async Task<HandlerResult<ContentType>> ImportContentTypeAsync(string json,
        CancellationToken cancellationToken = default)
    {
        var handlerResult = new HandlerResult<ContentType>();
        var package = JsonSerializer.Deserialize<ContentTypePackage>(json);

        if (package?.Type == null)
        {
            handlerResult.AddMessage(text["content_importInvalidJson"], ResultMessageType.Error);
            return handlerResult;
        }

        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var user = await currentUserAccessor.GetUserAsync(cancellationToken);

        // Import ContentType
        var export = package.Type;
        // Not tracked: the type is saved through SaveContentTypeAsync, the content below on this context
        var existing = await dbContext.ContentTypes.AsNoTracking().FirstOrDefaultAsync(x => x.Alias == export.Alias, cancellationToken);

        var contentType = existing ?? new ContentType { Alias = export.Alias };

        contentType.Name = export.Name;
        contentType.Description = export.Description;
        contentType.Icon = export.Icon;
        contentType.IsElementType = export.IsElementType;
        contentType.AllowAtRoot = export.AllowAtRoot;
        contentType.EnableListView = export.EnableListView;
        contentType.IncludeChildren = export.IncludeChildren;
        contentType.AvailableContentViews = export.AvailableContentViews;
        contentType.IsFolder = export.IsFolder;
        contentType.IsComposition = export.IsComposition;
        contentType.MediaIdAsString = export.MediaIdAsString;

        // Tabs and properties the type already has keep their ids, matched by alias: stored values point at the
        // property id, so a new id would leave every value behind on the next save (ZA-077). Anything new gets a new
        // id, so the same package imported into two sites can't clash with ids already used there. The system tab
        // keeps its fixed id.
        var existingTabIds = (existing?.Tabs ?? [])
            .GroupBy(t => t.Alias, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);
        var existingPropertyIds = (existing?.ContentProperties ?? [])
            .GroupBy(p => p.Alias ?? p.Name ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);

        var tabMap = new Dictionary<string, Guid>();
        foreach (var tab in export.Tabs)
        {
            // Each kept id once: a second tab or property with the same alias gets a new one
            var newId = tab.IsSystemTab
                ? tab.Id
                : existingTabIds.Remove(tab.Alias, out var keptTabId) ? keptTabId : Guid.NewGuid().NewSequentialGuid();
            tabMap[tab.Alias] = newId;
            tab.Id = newId;
        }

        contentType.Tabs = export.Tabs;

        // Set PropertyType TabIds based on map, keep or make ids, create alias map
        var propMap = new Dictionary<string, Guid>();
        foreach (var prop in export.ContentProperties)
        {
            var key = prop.Alias ?? prop.Name ?? string.Empty;
            var newId = existingPropertyIds.Remove(key, out var keptId) ? keptId : Guid.NewGuid().NewSequentialGuid();
            prop.Id = newId;
            propMap[key] = newId;
            if (!string.IsNullOrEmpty(prop.TabAlias) && tabMap.TryGetValue(prop.TabAlias, out var newTabId))
            {
                prop.TabId = newTabId;
            }
        }

        contentType.ContentProperties = export.ContentProperties;

        // Map CompositionAliases to Ids
        contentType.CompositionIds = [];
        foreach (var compAlias in export.CompositionAliases)
        {
            var comp = await dbContext.ContentTypes.FirstOrDefaultAsync(x => x.Alias == compAlias, cancellationToken);
            if (comp != null)
            {
                contentType.CompositionIds.Add(comp.Id);
            }
            else
            {
                handlerResult.AddMessage(text["content_importCompositionNotFound", compAlias], ResultMessageType.Warning);
            }
        }

        // Map AllowedChildContentTypeAliases to Ids
        contentType.AllowedChildContentTypes = new List<Guid>();
        foreach (var childAlias in export.AllowedChildContentTypeAliases)
        {
            var child = await dbContext.ContentTypes.FirstOrDefaultAsync(x => x.Alias == childAlias, cancellationToken);
            if (child != null)
            {
                contentType.AllowedChildContentTypes.Add(child.Id);
            }
            else
            {
                handlerResult.AddMessage(text["content_importChildTypeNotFound", childAlias], ResultMessageType.Warning);
            }
        }

        // Map ParentAlias to Id
        if (!string.IsNullOrEmpty(export.ParentAlias))
        {
            var parent = await dbContext.ContentTypes.FirstOrDefaultAsync(x => x.Alias == export.ParentAlias, cancellationToken);
            if (parent != null)
            {
                contentType.ParentId = parent.Id;
            }
            else
            {
                handlerResult.AddMessage(text["content_importParentNotFound", export.ParentAlias], ResultMessageType.Warning);
            }
        }

        var saveParams = new SaveContentTypeParameters { ContentType = contentType };
        var typeResult = await SaveContentTypeAsync(saveParams, cancellationToken);
        // What the file names but the site lacks (a composition, a child or parent type) goes back with the save's
        // result, so the editor sees it (ZA-260)
        typeResult.Messages.AddRange(handlerResult.Messages);
        if (!typeResult.Success)
        {
            return typeResult;
        }

        // If no content, done
        if (package.RootContents.Count == 0)
        {
            return typeResult;
        }

        // Import content recursively; its warnings (a language or property the site lacks) go back too (ZA-260)
        var contentWarnings = new HandlerResult<ContentType>();
        foreach (var rootExp in package.RootContents)
        {
            await ImportContentRecursive(rootExp, null, contentType, dbContext, user, propMap, contentWarnings);
        }

        typeResult.Messages.AddRange(contentWarnings.Messages);

        // One save for the imported content and its tag links, with the save hooks. It is a second save: the content
        // type was saved above on its own, so a content save that fails (a veto by an IBeforeEntitySave hook, a
        // database error) leaves the type imported without its content, and the result says it failed (ZA-184).
        // The token is not passed: once the type is saved, a caller that has gone does not stop its content.
        await ContentTagLinks.StageAsync(dbContext, extensionManager, dbContext.Contents.Local.ToList(),
            CancellationToken.None);
        var contentResult = await dbContext.SaveChangesAndLog(Array.Empty<Models.Content>(), null,
            new HandlerResult<Models.Content>(), cacheService, extensionManager, CancellationToken.None);
        if (!contentResult.Success)
        {
            typeResult.Success = false;
            typeResult.Messages.AddRange(contentResult.Messages);
        }

        return typeResult;
    }

    private async Task ImportContentRecursive(ContentExport exp, Guid? parentId, ContentType contentType,
        IZauberDbContext dbContext, User? user, Dictionary<string, Guid> propMap,
        HandlerResult<ContentType> handlerResult)
    {
        var content = new Models.Content
        {
            Name = exp.Name,
            Url = exp.Url, // May need uniqueness check
            ContentTypeId = contentType.Id,
            ContentTypeAlias = contentType.Alias,
            Published = exp.Published,
            Deleted = exp.Deleted,
            HideFromNavigation = exp.HideFromNavigation,
            InternalRedirectIdAsString = exp.InternalRedirectIdAsString,
            SortOrder = exp.SortOrder,
            ViewComponent = exp.ViewComponent,
            ParentId = parentId,
            // Nobody signed in records no user (D27); Guid.Empty would point at no account
            LastUpdatedById = user?.Id,
            DateCreated = DateTime.UtcNow,
            DateUpdated = DateTime.UtcNow
        };

        // Language
        if (!string.IsNullOrEmpty(exp.LanguageIsoCode))
        {
            var language = await dbContext.Languages.FirstOrDefaultAsync(l => l.LanguageIsoCode == exp.LanguageIsoCode);
            if (language != null)
            {
                content.LanguageId = language.Id;
            }
            else
            {
                handlerResult.AddMessage(text["content_importLanguageNotFound", exp.LanguageIsoCode, exp.Name],
                    ResultMessageType.Warning);
            }
        }

        // PropertyData
        content.PropertyData = new List<ContentPropertyValue>();
        foreach (var kvp in exp.PropertyData)
        {
            if (propMap.TryGetValue(kvp.Key, out var propId))
            {
                content.PropertyData.Add(new ContentPropertyValue
                {
                    ContentTypePropertyId = propId,
                    Value = kvp.Value,
                    Alias = kvp.Key // Optional
                });
            }
            else
            {
                handlerResult.AddMessage(text["content_importPropertyNotFound", kvp.Key, exp.Name],
                    ResultMessageType.Warning);
            }
        }

        dbContext.Contents.Add(content);

        // Build Path after save, but since we're saving at the end, might need to update later

        // Recurse children
        foreach (var childExp in exp.Children)
        {
            await ImportContentRecursive(childExp, content.Id, contentType, dbContext, user, propMap, handlerResult);
        }
    }
}
