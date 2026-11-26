# Block List Editor

**High-risk area.** Read this before touching `ZauberCMS.Components/Editors/BlockList*`, `ContentEditScope`,
`BlockPreview`, `ContentEditor.razor` (block mode and its save), `IContentService.SaveContentTreeAsync`
(`ContentService.Tree.cs`), `NestedContentTree`, `ContentExtensions.GetBlocks`, or block snapshots in
`ContentVersioningService`.

Redesigned in the 2026 rework (Roadmap Phase 9, D5): edits live in an edit session, the page and its blocks are saved
in one call, previews are static HTML. The old design (change sets bubbled up through callbacks, one save per block,
Blazor nodes moved into a shadow root) is gone; §6 lists what it got wrong and where each is fixed.

## 1. Data model (unchanged, no migration)

- Property value = JSON `List<Guid>` of block ids in `ContentPropertyValue.Value`. **Order lives only in that list.**
- Each block is a full `Content` row: `IsNestedContent = true`, `RelatedContentId = <the item whose list holds it>`
  (the page, or the block above it), element type (`ContentType.IsElementType`). Blocks can contain block lists.
- A block's `Published` switch is part of its values. "Save only" of a published page gives the page **and** its
  edited blocks a draft (`UnpublishedContentId`, as pages have).
- Limits: `NestedContentTree.MaxDepth = 20`, `NestedContentTree.MaxBlocks = 5000` per tree (a save over the limits is
  refused; a version stores the first 5000).

## 2. How Core finds blocks: `INestedContentReferenceProvider`

Core never guesses which values hold blocks. Each property's editor alias (`PropertyType.ComponentAlias`) is matched
to a discovered `INestedContentReferenceProvider` (`EditorAlias`, `GetReferencedIds(value)`, `RemapIds(value, map)`).
The Block List editor's (`BlockListNestedContentReferenceProvider`, alias `ZauberCMS.BlockListEditor`) is built in; a
site's own provider for the same alias wins. Properties of compositions count. `NestedContentTree` (internal) walks a
tree through the providers, level by level, and is used by:

| Operation | What it does with the tree |
| --- | --- |
| `SaveContentTreeAsync` | saves the blocks the new values reach, deletes the ones no longer reached |
| `DeleteContentAsync` (permanent) | deletes the blocks of every deleted item, those its draft reaches too |
| `ClearUnpublishedContentAsync` | drops the blocks' drafts and deletes blocks only the draft reached |
| `CreateVersionAsync` | snapshots every block (a draft version records the blocks' drafts) |
| `PublishVersionAsync` (restore) | writes the version's blocks, drops their drafts, deletes blocks the version doesn't have |
| `CopyContentAsync` | clones every block with a new id and rewrites the values (`RemapIds`) |

Only rows with `IsNestedContent` are ever treated as blocks: a page id in a list is skipped, never saved, deleted,
snapshotted or cloned.

## 3. Editing: `ContentEditScope`

| Step | What happens |
| --- | --- |
| Open a page | `ContentEditor` loads it into `WorkingCopy` and makes a root `ContentEditScope`, cascaded to its property editors |
| List loads | `BlockListEditorProperty` reads the ids from its value; each block comes from the scope (`GetDraft`) or the database (with its stored draft applied) |
| Open block B1 | the list opens a side-panel `ContentEditor` (`IsBlockList`) with `EditScope = scope.BeginChild()`; that editor cascades its child scope to its own lists |
| Open B2 inside B1 | same again, one level down |
| Save in B2 | `EditScope.SetDraft(WorkingCopy)`, close Ok; B1's list `Commit()`s the child scope (B2's values move into B1's scope) and updates its value if B2 is new |
| Cancel (or close) | the list `Rollback()`s the child scope: everything done inside, deeper blocks included, is gone |
| Save the page | `SaveContentTreeAsync(WorkingCopy, RootScope.Drafts, SaveUnpublishedOnly)`; on success one `NotifyContentSaved(page)`, and the editor reloads from the database with a fresh scope |

The scope only holds edited blocks. Which blocks a list holds, and their order, is the list's value on the item that
owns it, so removing a block is just removing its id: the save deletes it (and everything below it) once nothing
reaches it. A draft left behind by a cancelled or removed edit is not reached and is ignored.

`ContentEditor` loads once per (`ContentId`, `ParentId`, `Content`, `EditScope`): stacked side panels hand their
parameters in again on every render, and reloading would drop the panel's edits.

## 4. Saving: `IContentService.SaveContentTreeAsync`

One scope, one DbContext, one transaction:

0. Refuse a stale copy: the caller's `UnpublishedContentId` (and draft `DateUpdated`, when it sends the draft) must be
   the stored one, and a "Save only" also checks the page's `DateUpdated`. A draft doesn't change `DateUpdated`, so
   without this a second editor's "Save only" would overwrite the first one's draft and delete the blocks it added.
1. Walk what the stored page reaches now, live and through drafts: its own, and those its blocks still have (`before`).
2. Walk what the new values reach (`after`), reading each block from the caller's drafts or the database. Refused: a
   tree over the limits, a "block" that is a page, an existing block not in `before` (it belongs to other content).
3. Stage the page (`StageContentAsync`, the same checks as `SaveContentAsync`), or its draft in "Save only".
4. Stage the blocks, parents first. Publish: edited blocks are saved live (concurrency-checked on `DateUpdated`) and
   their drafts removed; unedited blocks with a draft from an earlier "Save only" are published. "Save only": edited
   existing blocks get a draft, new blocks are inserted (only the draft reaches them, so the live site doesn't).
5. Delete `before − after` (in "Save only", the live tree stays reached): property values, drafts, rows.
6. One audit row (not for drafts), one `SaveChangesAndLog` that runs the Content save hooks for the page and every
   block row it writes, publishes or deletes (drafts are not rows: no hooks; a new block in a "Save only" is a row, so
   it does fire "Added"), commit, then **one** version of the page, taken after the blocks are written.

It raises no `AppState` events; the editor raises one for the page. `AllowedElementTypeIds` is still a warning only,
now checked for new blocks too. `SaveContentAsync` (one item) publishes a page's own draft but leaves its blocks'
drafts; the next tree save publishes them (step 1 walks them), and "Clear changes" drops them. Clear changes and
version restore delete and rewrite blocks without running the save hooks per block.

Restoring a version writes the version's blocks, drops the drafts of every block the restored values reach, and
deletes only blocks the restored values no longer reach (a version made by the old editor may lack snapshots of
blocks its save added; those are kept as stored).

## 5. Previews: `BlockPreview`

The site's `IContentBlockPreview` (matched by `ContentTypeAlias`, else `ContentBlockPreviewFallback`) is rendered to
HTML on the server with `HtmlRenderer` and the circuit's services, one preview at a time per circuit. A preview is a
picture: it is `inert` (clicks and links in it do nothing, so a link can't navigate away from unsaved edits; edit with
the pencil), and it has no interactivity, no `OnAfterRender`/JS, no admin cascading values; scripts in it don't run.
With the editor's `Stylesheets` set, `block-preview.js` puts the HTML in a shadow root with those stylesheets (site CSS
styles the preview and nothing else); without them it sits in the page and takes the admin's CSS. No global `<link>`.
A preview re-renders only when its block, or a block inside it, was edited (a new preview copy); reorder and remove
reuse the rendered previews, and a render that finishes after a newer one is dropped.

The preview gets `ContentEditScope.CreatePreviewCopy(block)`: a copy whose `PendingBlockListChanges[alias]` holds the
unsaved blocks whose ids appear in that property's value (any format), copies of their own all the way down. **A
preview only shows unsaved nested blocks if it asks for them:** call
`Content.GetBlocks(alias, contentService, includePendingChanges: true)` (or read `PendingBlockListChanges` yourself
for a custom format): unsaved blocks from the copy, the rest from the database as the editor shows them (stored drafts
applied, unpublished included). A preview that reuses a front-end block view, which calls `GetBlocks` without the
flag, shows saved, published values below it. Front-end views call `GetBlocks` without the flag: one level, in order,
published only, live values.

Checked in a browser at four levels (FAQ → item → FAQ → item, the third level with a site stylesheet): each panel's
preview shows the unsaved level below before anything is saved, cancel at level 2 drops levels 3 and 4, and after
"Save only" and a reload the level-3 preview shows the level-4 draft.

## 6. Invariants and the old failure modes

| # | Invariant | Old failure (audit id) | Now |
| --- | --- | --- | --- |
| 1 | A block row exists iff a saved value (live or draft) reaches it | cancelled adds, removed subtrees left as orphans (F4, ZA-037) | tree save deletes `before − after` |
| 2 | Nothing is stored until the page is saved, and that save is all or nothing | per-block saves, partial trees (F5, ZA-009) | one transaction |
| 3 | Cancel at any level discards everything done inside it | edits merged up immediately (F1, ZA-034) | child scope rollback |
| 4 | Block edits follow the page's publish mode | "Save only" saved blocks live (F6, ZA-038) | block drafts |
| 5 | Removing a block removes its whole subtree | as 1 | as 1 |
| 6 | A version holds the page and its blocks as saved | snapshot taken before blocks were saved (F8, ZA-008) | version after commit |
| 7 | After a save the editor equals the database | stale `DateUpdated`, kept change lists (F7, ZA-039) | reload with a fresh scope |
| 8 | Every block list of an item is saved | root replaced pending changes (F2, ZA-035) | one draft map, every value walked |
| 9 | The newest edit of a block wins | update dropped when pending as added (F3, ZA-036) | scope overwrite |
| 10 | Blazor-owned nodes never live in a shadow root; site CSS never reaches the admin | nodes moved into shadow root, global `<link>`s (F13, ZA-043, ZA-010) | static previews |
| 11 | Notifications name the content and run on the owning circuit | `NotifyContentDeleted(null)` refreshed every editor (F9, ZA-040) | delete and version restore name the item; an editor reloads only for its own item (on `InvokeAsync`) and warns when that drops unsaved block edits; a panel whose page was reloaded says its edit was not kept |
| — | Edits in a stacked panel survive re-renders | `OnParametersSetAsync` rebuilt the working copy (F12, ZA-042) | load once per parameters |
| — | No re-entrant bubbling | changes merged 2^(depth−1) times (F10, ZA-041) | no callbacks |

## 7. Extending: an editor that hosts nested content

1. Implement `IContentProperty` as usual; store your blocks' ids in the value in any shape.
2. Implement `INestedContentReferenceProvider` for your editor's alias, so Core saves, deletes, versions and copies
   your blocks with the page.
3. Take `[CascadingParameter] ContentEditScope? Scope`. Read a block with `Scope.GetDraft(id)` before the database;
   open a block's editor with `ContentEditor` (`IsBlockList = true`, `EditScope = Scope.BeginChild()`), then
   `Commit()` on Ok and `Rollback()` otherwise; change only your value (`ValueChanged`) for adds, removes and order.
4. For previews use `BlockPreview` with `Scope.CreatePreviewCopy(block)`.

Don't save blocks yourself, and don't use the obsolete `ISupportsBlockListChanges` / `BlockListChanges` /
`ContentEditor.OnBlockListChanges` (they are never called).

## 8. Manual check after changes

3-level nested add → reorder → edit a nested block twice before saving → delete a block with children → Cancel
inside a nested panel → save → reload: order, values, deletions and the cancel all hold; no orphan rows; front end
matches; "Save only" leaves the live page alone and the editor shows the draft; "Clear changes" drops it; restoring
an older version restores its blocks; previews update before save; two block-list properties on one type both
persist. Tests: `ContentTreeSaveTests` (Core, all three databases), `ContentEditScopeTests`, `BlockListStateTests`.
