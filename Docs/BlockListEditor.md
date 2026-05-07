---
description: Deep dive on the BlockListEditor — nested editors, live previews, shadow DOM, persistence, versioning
alwaysApply: true
---

# BlockListEditor

Read this **before** changing anything in `ZauberCMS.Components/Editors/BlockList*`, `ContentEditor.razor`'s block-list code paths, `ProcessBlockListEditorChangesAsync`, or block versioning. The BlockListEditor is the single most architecturally complex piece of ZauberCMS — nesting, live preview, shadow DOM, and snapshot versioning all interact, and most failure modes are silent.

## Summary

A BlockListEditor property holds an ordered list of **block content items**. Each block is a full `Content` row in `ZauberContents` flagged `IsNestedContent = true`. Blocks can themselves contain BlockListEditor properties — nesting is unbounded ("inception" style). The admin UI renders each block's preview into its own **open shadow DOM root** so consumer-supplied front-end CSS can style previews without leaking into the admin.

The four things that make this complex and easy to break:

1. **Nesting** — each level is a separate `BlockListEditorProperty` instance with its own immutable state; changes propagate up via callback chain.
2. **Live preview** — edits made in nested modal dialogs need to render in the parent's preview *before* save. Done via an in-memory `Content.PendingBlockListChanges` dictionary.
3. **Shadow DOM** — applies at the **outermost** block boundary only; nested previews share the parent block's shadow root.
4. **Versioning** — the version snapshot recursively captures the full nested tree, so rolling back an old version restores blocks that may have been deleted since.

## Data model & storage shape

Each BlockListEditor property value is a JSON array of GUIDs:

```json
["1f2a...", "5b8c...", "9e3d..."]
```

Stored in `ContentPropertyValue.Value` (string, no length limit), see [ContentPropertyValue.cs:16](../ZauberCMS.Core/Content/Models/ContentPropertyValue.cs#L16).

**Nesting is by ID reference, never by JSON nesting.** Every block is its own `Content` row. A nested block list inside Block A simply means Block A's `Content` row has its own BlockListEditor property whose `ContentPropertyValue.Value` holds another array of GUIDs:

```
Parent Content (top-level page)
  └─ property "blocks"        →  [B, C]
       Content B (IsNestedContent=true)
         └─ property "subBlocks"  →  [D, E]
              Content D (IsNestedContent=true)
                └─ property "leafBlocks" → [F]
                     Content F (IsNestedContent=true)
```

`IsNestedContent` (see [Content.cs:88-91](../ZauberCMS.Core/Content/Models/Content.cs#L88-L91)) excludes blocks from regular content queries. Block lookup uses `QueryContentParameters { NestedFilter = NestedContentFilter.Only }`.

## Render tree (admin)

```
BlockListEditorProperty       ← outer Razor component, one per property on a content type
 ├─ BlockListDropZone           ← insert / reorder target (between every pair of items)
 └─ BlockListItem (per block)
     └─ RenderBlock              ← initialises shadow DOM, dispatches to preview
         └─ <DynamicComponent>   ← resolves matching IContentBlockPreview by class name
             └─ user-supplied IContentBlockPreview (or ContentBlockPreviewFallback)
```

Files:
- [BlockListEditorProperty.razor](../ZauberCMS.Components/Editors/BlockListEditorProperty.razor) — main component, change-tracking state, modal launch.
- [BlockListItem.razor](../ZauberCMS.Components/Editors/Components/BlockListItem.razor) — single block UI, drag handle, edit/delete actions.
- [BlockListDropZone.razor](../ZauberCMS.Components/Editors/Components/BlockListDropZone.razor) — drop target between items.
- [RenderBlock.razor](../ZauberCMS.Components/ContentComponents/RenderBlock.razor) — preview dispatcher + shadow DOM init.
- [ContentBlockPreviewFallback.razor](../ZauberCMS.Components/ContentComponents/ContentBlockPreviewFallback.razor) — default when no preview class matches.

State is kept in an immutable record:

- [BlockListState.cs](../ZauberCMS.Components/Editors/Models/BlockListState.cs) — `Items`, `AddedItems`, `UpdatedItems`, `DeletedItems`, plus the critical `MergeNestedChanges` method (lines 193-246).
- [BlockListChanges.cs](../ZauberCMS.Core/Content/Models/BlockListChanges.cs) — DTO carrying Added/Updated/Deleted lists between levels.

## Nested editors ("inception")

Each level is a fresh `BlockListEditorProperty` instance. There is **no shared mutable state** between levels — only an event-callback chain.

Editing a block opens `ContentEditor` in a modal with three flags ([BlockListEditorProperty.razor:236-312](../ZauberCMS.Components/Editors/BlockListEditorProperty.razor#L236-L312)):

```csharp
{ nameof(ContentEditor.IsBlockList),       true                                    },
{ nameof(ContentEditor.NestedParentId),    Content!.Id                             },
{ nameof(ContentEditor.OnBlockListChanges), new Action<BlockListChanges>(HandleNestedChanges) }
```

If that block's content type itself contains a BlockListEditor property, the modal renders another `BlockListEditorProperty` — and so on, recursively.

### Change propagation

When a deeply-nested edit happens, changes bubble upward one level at a time:

```
Level 3 BlockListEditorProperty
  edit/add/delete → State.AddItem/UpdateItem/DeleteItem
  → NotifyChanges()
       → ChangesPending.Invoke(changes)   ← passed to its parent ContentEditor as OnBlockListChanges

Level 2 ContentEditor receives changes
  → HandleBlockListChanges() stores in PendingBlockListChanges
  → on its block list editor (Level 2 BlockListEditorProperty), HandleNestedChanges fires
       → State.MergeNestedChanges(nestedChanges)
       → NotifyChanges()
       → ChangesPending.Invoke(nestedChanges)   ← bubbles to its own parent

Level 1 ContentEditor receives changes
  → … same pattern, all the way to the root
```

The merge logic in `BlockListState.MergeNestedChanges` ([BlockListState.cs:193-246](../ZauberCMS.Components/Editors/Models/BlockListState.cs#L193-L246)) handles dedup and avoids marking newly-added items as Updated.

### `async void HandleNestedChanges` — keep the try/catch

`OnBlockListChanges` is typed `Action<BlockListChanges>` (synchronous) on `ContentEditor`, but `HandleNestedChanges` needs to do async work. `async void` is the only option, and **without** the try/catch any exception bubbles to the synchronization context and **tears down the Blazor circuit** — the entire admin tab dies. Don't strip the catch:

```csharp
// async void is required because OnBlockListChanges on ContentEditor is typed Action<BlockListChanges>.
// Without try/catch an exception here would propagate to the SynchronizationContext and tear down the Blazor circuit.
private async void HandleNestedChanges(BlockListChanges nestedChanges)
{
    try
    {
        State = State.MergeNestedChanges(nestedChanges);
        await NotifyChanges();
        ChangesPending?.Invoke(nestedChanges);
        StateHasChanged();
    }
    catch (Exception ex)
    {
        Logger.LogError(ex, "Failed to handle nested block list changes");
    }
}
```

See [BlockListEditorProperty.razor:314-329](../ZauberCMS.Components/Editors/BlockListEditorProperty.razor#L314-L329).

## Live preview & `PendingBlockListChanges`

The challenge: when a user edits a nested block in a modal, they expect the parent's preview to update immediately — but the change isn't in the database yet. Solution: an in-memory dictionary on `Content` itself.

```csharp
// Content.cs:196-198
[NotMapped]
[JsonIgnore]
public Dictionary<string, Dictionary<Guid, Content>> PendingBlockListChanges { get; set; } = new();
//          ^ keyed by property alias       ^ keyed by block content ID
```

`BlockListEditorProperty.NotifyChanges()` populates this dictionary on every edit ([BlockListEditorProperty.razor:331-350](../ZauberCMS.Components/Editors/BlockListEditorProperty.razor#L331-L350)).

Preview components read it via the `includePendingChanges: true` overload of `GetBlocks`:

```csharp
// In an IContentBlockPreview component (admin only)
FaqItems = await Content.GetBlocks("FAQs", ContentService, includePendingChanges: true);

// In an IContentView (front-end), use the no-flag overload
FaqItems = await Content.GetBlocks("FAQs", ContentService);
```

See [ContentExtensions.cs:116-172](../ZauberCMS.Core/Extensions/ContentExtensions.cs#L116-L172).

**Rule**: admin-side previews always pass `includePendingChanges: true`. Front-end views must not — pending changes never persist outside a single editing session and don't exist on the front-end's `Content` instances.

## Shadow DOM & previews

[RenderBlock.razor](../ZauberCMS.Components/ContentComponents/RenderBlock.razor) attaches an open shadow root on first render and injects the per-editor stylesheets configured in `BlockListEditorSettingsModel.Stylesheets`:

```csharp
protected override async Task OnAfterRenderAsync(bool firstRender)
{
    if (firstRender && Stylesheets.Count > 0)
    {
        await JsRuntime.InvokeVoidAsync("initializeShadowDOMWithMultipleStylesheets", _componentDiv, Stylesheets);
    }
}
```

JS side: [shadow-dom.js](../ZauberCMS.Components/wwwroot/js/shadow-dom.js) calls `attachShadow({ mode: 'open' })`, then loads each stylesheet as a `<link>` inside the shadow root (with a module-level cache to avoid duplicate fetches across blocks).

### Important: only the outermost block has its own shadow root

A block's preview component may itself render nested block previews (e.g. an `FAQ` preview that lists `FAQItem` previews inside it). Those nested previews **share the outer block's shadow root** — they do not get their own shadow boundaries.

This is by design: from the user's perspective, the entire preview of one block is a single isolated visual unit. Don't try to wrap nested previews in their own `RenderBlock` shadow root; you'll fragment styling and lose CSS inheritance the consumer's stylesheet relies on.

## Save flow

### Client side

`BlockListEditorProperty.NotifyChanges` serializes the property value as `JsonSerializer.Serialize(List<Guid>)` — only the ordered IDs. Block content itself stays in the in-memory state until save.

### Server side

`ContentService.SaveContentAsync` saves the parent first, then `ProcessBlockListEditorChangesAsync` recursively walks the tree:

```csharp
// ContentService.cs:1951-2020 (paraphrased)
private async Task ProcessBlockListEditorChangesAsync(Content content, ...)
{
    // 1. Find all block-list properties on this content type
    var blockListPropertyIds = contentType.ContentProperties
        .Where(p => p.ComponentAlias == "ZauberCMS.BlockListEditor")
        .Select(p => p.Id).ToHashSet();

    // 2. Parse each one to get the IDs it references
    foreach (var property in blockListProperties)
    {
        var contentIds = JsonSerializer.Deserialize<List<Guid>>(property.Value);
        // …gather all referenced block content rows
    }

    // 3. Recursively save each block (which itself may have block-list properties)
    foreach (var nestedContent in nestedContents)
    {
        await SaveContentAsync(nestedSaveParams, cancellationToken);
    }
}
```

See [ContentService.cs:65-257](../ZauberCMS.Core/Content/Services/ContentService.cs#L65-L257) and [ContentService.cs:1951-2020](../ZauberCMS.Core/Content/Services/ContentService.cs#L1951-L2020). Parent + all descendants are saved within one logical operation.

`ContentEditor.ProcessBlockListChanges` ([ContentEditor.razor:608-678](../ZauberCMS.Components/Admin/ContentSection/ContentEditor.razor#L608-L678)) deduplicates the Added/Updated/Deleted lists and resolves overlap (e.g. an item that was added then deleted is dropped entirely) before passing to the service.

## Versioning

Versioning is **snapshot-based**, in a separate `ZauberContentVersions` table. A `ContentVersion` carries:

- `Snapshot` — the parent content metadata at the time
- `PropertySnapshots` — the parent's property values
- `BlockListSnapshots` — **a recursive capture of every nested block** (`List<BlockListContentSnapshot>`)

See [ContentVersion.cs](../ZauberCMS.Core/Content/Models/ContentVersion.cs).

Snapshot creation walks the block tree recursively with cycle protection:

```csharp
// ContentVersioningService.cs:621-689
private static async Task CreateBlockListSnapshotsRecursiveAsync(
    IZauberDbContext dbContext, Content content,
    List<BlockListContentSnapshot> snapshots,
    HashSet<Guid> processedContentIds)   // ← cycle guard
{
    // Parse each block-list property, load referenced contents,
    // snapshot each, then recurse into each block's own block-list properties.
}
```

Publishing an older version calls `RestoreBlockListContentAsync` ([ContentVersioningService.cs:691-800](../ZauberCMS.Core/Content/Services/ContentVersioningService.cs#L691-L800)) which:

- For blocks that still exist: overwrites their `Name` and `PropertyData` from the snapshot.
- For blocks that were deleted since: **recreates them from the snapshot**, including the original `Id`.

This means a soft-deleted block can be brought back by publishing an older version — there is no "permanently lost block" state until the row itself is hard-deleted (which only happens in orphan cleanup; see Known gaps).

Cache invalidation on version operations: `cacheService.ClearCachedItemsWithPrefix($"Content_{content.Id}")` — by parent content ID prefix only.

## Front-end read path

Consumers use `Content.GetBlocks(alias, ContentService)` to read a block list at runtime:

```csharp
// ContentExtensions.cs:116-172 (paraphrased)
public static async Task<IEnumerable<Content>> GetBlocks(
    this IHasPropertyValues content, string alias, IContentService contentService)
{
    var ids = content.GetValue<List<Guid>>(alias);
    if (ids is null || ids.Count == 0) return [];

    var blockList = await contentService.QueryContentAsync(new QueryContentParameters
    {
        Ids = ids,
        AmountPerPage = 150,
        NestedFilter = BaseQueryContentParameters.NestedContentFilter.Only
    });

    // Preserve original ordering from the JSON array
    var dict = blockList.Items.ToDictionary(x => x.Id, x => x);
    return ids.Where(dict.ContainsKey).Select(id => dict[id]);
}
```

Notes:
- Returns `IEnumerable<Content>` — not typed models. Callers extract values with `block.GetValue<T>("propAlias")`.
- One level only. To read a grandchild block list, the caller calls `GetBlocks` again on each child.
- Order is preserved by dict lookup against the JSON array.

## Adding a new block content type

1. Create an Element Type in admin (`OnlyElementTypes = true` on the content type). Element types don't appear in the content tree — they exist only to be used as blocks.
2. Optionally, restrict which element types are allowed in a particular block list by setting `BlockListEditorSettingsModel.AllowedElementTypeIds` on that property's settings.
3. Optionally, supply a custom preview: implement `IContentBlockPreview` (the alias-to-preview match is by **class name**, not file name — see how `IContentView` works in [.claude/CLAUDE.md](../.claude/CLAUDE.md)). With no custom preview, `ContentBlockPreviewFallback` shows the block's name and content type.
4. For consumer-side rendering on the front-end, the consumer's site project provides a normal Blazor component that calls `Content.GetBlocks(alias, ContentService)` and renders each block however it wants.

See [FAQ.razor](../ZauberCMS.Web/ContentBlockPreviews/FAQ.razor) for a worked example of a preview that itself reads a nested block list.

## Known gaps / gotchas

These are real, currently un-addressed limitations. Don't try to "fix" them in passing — they each need a design decision.

### 1. Settings key typo: `Styleheets`

[BlockListEditorSettingsModel.cs:5](../ZauberCMS.Components/Editors/Models/BlockListEditorSettingsModel.cs#L5) and [BlockListEditorSettings.razor:10](../ZauberCMS.Components/Editors/Settings/BlockListEditorSettings.razor#L10) both use `Styleheets` (missing a "t"). Renaming is breaking for any settings JSON already stored in `ContentTypeProperty.Settings`. If a future change renames it, read both keys for back-compat.

### 2. Reflection-based wiring of `ChangesPending`

[DynamicContentProperty.razor:27](../ZauberCMS.Components/Admin/DynamicContentProperty.razor#L27):

```csharp
if (ChangesPending != null && ComponentType?.Name == "BlockListEditorProperty")
{
    parameters["ChangesPending"] = ChangesPending;
}
```

The dispatcher hard-codes the class name as a string. Renaming `BlockListEditorProperty` (or replacing it with a different implementation) silently breaks nested change propagation — there is no compile error, no runtime error, edits just stop bubbling up. A marker interface (e.g. `ISupportsBlockListChanges`) would be a sounder fix; not worth doing as a drive-by but worth knowing about.

### 3. Orphan cleanup runs only on ContentType deletion

`ContentService.DeleteContentTypeAsync` ([ContentService.cs:659-737](../ZauberCMS.Core/Content/Services/ContentService.cs#L659-L737)) detects nested content rows no longer referenced by any property value and force-deletes them. **This is the only place orphan cleanup runs.** Other actions that can leave orphans:

- Removing a BlockListEditor property from a ContentType (the property goes away; the nested rows it referenced stay).
- Renaming a property alias.
- Manually editing the JSON value in the DB.

These leak nested `Content` rows with `IsNestedContent = true` that no living property references.

### 4. Cache invalidation is parent-prefix only

Block content cache keys are scoped to their parent (`Content_{parentId}_*`). The standard edit flow (open the parent → edit nested block → save) invalidates correctly because the parent gets re-saved. But any out-of-band write to a nested block (e.g. via a custom service call) won't invalidate the parent's cached query.

### 5. `GetBlocks` does one level only

Easy to forget when writing a preview that consumes nested blocks. There is no recursive "get me everything" API — recursion is the caller's responsibility.

## When making changes — minimum verification

If you touch any code in `ZauberCMS.Components/Editors/BlockList*`, `RenderBlock.razor`, `ProcessBlockListEditorChangesAsync`, `Content.PendingBlockListChanges`, or `ContentExtensions.GetBlocks`, run through this checklist by hand in `ZauberCMS.Web` against `app.db`:

1. **Two-level edit cycle**: open a content item that uses a block list, add a block whose type contains its own block list, add a sub-block, save. Reload and confirm everything persisted at both levels.
2. **Three-level live preview**: with the parent open, edit a sub-block in a modal, save the modal but **don't save the parent yet**. Confirm the parent's preview updates before save (this exercises `PendingBlockListChanges`).
3. **Reorder**: drag-reorder blocks; confirm the JSON `List<Guid>` order in the DB matches what you see.
4. **Delete + version restore**: delete a block, save the parent, then republish a previous version from the version history. The deleted block should reappear (this exercises `RestoreBlockListContentAsync`).
5. **Shadow DOM isolation**: configure a custom stylesheet in the editor settings, give it a rule that conflicts with admin styling (e.g. set body colour). Confirm the rule applies inside the block preview but doesn't bleed into admin chrome.
6. **Front-end render**: in the consumer view that renders this content, confirm `Content.GetBlocks("alias", ContentService)` (without `includePendingChanges`) returns the saved blocks in order.
