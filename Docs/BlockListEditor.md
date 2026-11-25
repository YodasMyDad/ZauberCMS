# Block List Editor

**Highest-risk area of the codebase.** Read this before touching `ZauberCMS.Components/Editors/BlockList*`,
`ContentEditor.razor` block handling (`HandleBlockListChanges`, `ProcessBlockListChanges`, `IsBlockList` mode),
`DynamicContentProperty` (`ISupportsBlockListChanges`), `RenderBlock.razor`/`shadow-dom.js`,
`ContentExtensions.GetBlocks`, or block snapshots in `ContentVersioningService`.

Status: the current design (change sets bubbled up through callbacks, per-block saves, Blazor nodes moved into a shadow
root) has known data-loss bugs (§5). The agreed direction is §6. **Don't add features to the callback chain or copy it
into new editors** — fix within it only, or implement the §6 redesign.

## 1. Data model

- Property value = JSON `List<Guid>` of block IDs in `ContentPropertyValue.Value`. **Order lives only in that list.**
- Each block is a full `Content` row: `IsNestedContent = true`, `RelatedContentId = <owning content id>`,
  `Published = true`, element type (`ContentType.IsElementType`). Blocks can contain block lists.
- Limits: `MaxBlockListNestingDepth = 20`, `MaxSnapshotsPerVersion = 5000`.
- `AllowedElementTypeIds` validation on save is warning-only and runs before new blocks exist (so new blocks are
  effectively unchecked server-side).
- Core code that walks block trees (delete cascade, version snapshots, copy, validation) **assumes "a JSON array of
  GUIDs"**. Any editor storing nested IDs in another shape gets no cascade delete, no snapshots and shared (not
  cloned) blocks on copy.

## 2. How an edit flows today (R → B1 → B2 → B3, all existing)

| Step | What happens | State |
| --- | --- | --- |
| Open root | `ContentEditor` (E0) loads R → `WorkingCopy` W0; `BlockListEditorProperty` (BLEP0) loads B1 into `BlockListState.Items` | W0 deep copy, but its `PendingBlockListChanges` inner dictionaries are copied shallowly |
| Open B1…B3 | `BlockListState`'s `OpenContentEditor` opens a stacked side panel `ContentEditor` (E1…E3) with `Content = Bn` (same object), `NestedParentId`, `OnBlockListChanges = parentBLEP.HandleNestedChanges` | each En builds its own Wn |
| Edit in B3 | `UpdateProperty` changes W3 only | |
| Close B3 (Save) | `IsBlockList` → `CloseAsync(Ok(W3))`; **nothing persisted** | |
| Back in BLEP2 | new B3′ copied from W3 → `UpdateItem` → `NotifyChanges`: writes W2's JSON, sets `W2.PendingBlockListChanges[alias]`, calls `ChangesPending` → E2 → BLEP1 `HandleNestedChanges` (`async void`) → `MergeNestedChanges` → `NotifyChanges` … up to E0 | B3′ is one shared object across all levels' lists |
| Close B2, B1 | same bubbling | root ends with `Updated = [B3′, B2′, B1′]` (children first) |
| Save root | `SaveContentAsync(W0)` (own transaction; **R's version snapshot taken here, from the DB = old blocks**) → `ProcessBlockListChanges`: `SaveContentAsync` per added/updated block, `DeleteContentAsync(MoveToRecycleBin = false)` per deleted block (hard delete + nested cascade), each in its own transaction with its own audit row, version and cache clear → pending set to null → `NotifyContentSaved` → refresh | only now is anything written |

Previews: `IContentBlockPreview` components call `GetBlocks(alias, contentService, includePendingChanges: true)`, which
returns only pending items when any exist (no DB merge). `RenderBlock` attaches a shadow root (only when the editor has
`Stylesheets`) and `shadow-dom.js` **moves Blazor-rendered nodes into it**. Front-end uses `GetBlocks` without the flag
(one level, ordered, unpublished excluded).

## 3. Invariants (any change must preserve or move towards these)

1. A block row exists **iff** some saved property JSON on its parent references it — no orphans, no dangling IDs.
2. Nothing reaches the DB until the root save, and the root save is **all-or-nothing**.
3. Cancel at any level discards every change made inside that modal, including descendants.
4. Block changes follow the parent's publish mode: a draft ("Save only") must not change the live site.
5. Removing a block removes its whole subtree, pending and persisted.
6. A version snapshot reflects the state **after** the save, parent and blocks together.
7. After a successful save, editor state equals the DB (fresh `DateUpdated`, empty change lists).
8. Multiple block-list properties (same content, compositions, any depth) are independent and all persisted.
9. Block IDs are unique in the tree; lists never share a block; copy clones the tree with new IDs.
10. Blazor-owned nodes never live inside a shadow root; front-end CSS never reaches the admin.
11. Content notifications name the affected root content (never `null`) and are handled on the owning circuit's thread.

## 4. Rules for changes today

- Keep the `try/catch` in `async void HandleNestedChanges` — an escaped exception kills the circuit.
- `NotifyChanges` must keep writing **every** item to `PendingBlockListChanges` (previews drop IDs missing from it).
- Run the manual check (§7) after any change in this area; there are no automated tests.
- New editors that host nested content: don't implement `ISupportsBlockListChanges` and the bubbling pattern — raise
  it first; build on the §6 session design instead.

## 5. Known failure modes (tracked in [.audit/report.md](../.audit/report.md) ZA-008…ZA-010, ZA-034…ZA-043)

| Id | Failure | Trigger |
| --- | --- | --- |
| F1 | **Cancel leaks** — changes merge into ancestors immediately; `Cancel` only clears its own copy | open B1, delete nested B2, Cancel, save R → B2 hard-deleted while B1 JSON still references it |
| F2 | Root **replaces** pending changes instead of merging (`HandleBlockListChanges`), and each level bubbles twice | two block-list properties on one type → one property's new blocks never inserted |
| F3 | `MergeNestedChanges` drops an update when the item is already in Added | add B1 with new B2, reopen, edit B2, save → B2 saved with old values |
| F4 | Deleting a block keeps its pending descendants | new descendants inserted as orphans |
| F5 | Partial save — blocks processed even if parent save failed; no overall transaction; pending cleared on failure | concurrency conflict on parent |
| F6 | "Save only" on published content saves blocks live | block edits go live before publish |
| F7 | Stale state after save (old `DateUpdated`, change lists kept) — *needs repro* | save, edit a block again, save → false concurrency error / failed delete |
| F8 | Version N has parent N with blocks N−1; ~2N+2 cache clears per save | restore/publish version N reverts its block edits |
| F9 | Hard delete raises `NotifyContentDeleted(null)` on the singleton → every open editor in every session refreshes, losing unsaved edits | any block delete while others edit |
| F10 | Bubbling is re-entrant and grows 2^(depth−1) | deep nesting → slow, racy |
| F12 | `OnParametersSetAsync` rebuilds `WorkingCopy` each time — *needs repro* | stacked modal re-supplies params → in-modal edits reset |
| F13 | Shadow DOM: Blazor nodes moved into shadow root (new nodes may not render; clicks in previews don't fire); no isolation without stylesheets; stylesheets also leak as global `<link>` | preview switching from placeholder to list |

## 6. Simplification direction (planned — not implemented)

1. **`IContentEditSession`** (cascading, one per root editor): `Guid → draft + parent + alias + state`, with
   `BeginScope/Commit/Rollback` per modal, `GetChildren`, `OpenChildEditorAsync`, `Remove` (subtree), `SetOrder`.
   Replaces callback bubbling and `PendingBlockListChanges` (kept as `[Obsolete]` shims). Fixes F1–F4, F7, F10, F12.
2. **`IContentService.SaveContentTreeAsync`**: one context + one transaction; prune unreachable drafts; parents first;
   validate; respect publish mode (blocks get drafts via existing `UnpublishedContentId`); **one** version after
   children; one cache clear; one notification. Additive API, no schema change. Fixes F5, F6, F8, F9, F11.
3. **Static previews**: render preview HTML with `HtmlRenderer` and set `shadowRoot.innerHTML` via JS (Blazor never
   owns those nodes); remove global `<link>`s. Fixes F13.
4. **`INestedContentReferenceProvider`** (discovered): `GetReferencedIds(value)` / `RemapIds(value, map)` per editor
   alias, used by delete cascade, snapshots, copy and validation — lets future editors (grid/layout) store any shape.
5. If 1–3 slip, stopgaps: merge per alias at root, remove the duplicate `ChangesPending` call, fix Added-merge,
   drop descendants on delete, skip blocks when parent save fails, remount after save, notify with root content.

Not recommended now: storing blocks inline in the parent JSON (changes persisted format; needs data migration).

## 7. Manual check after changes

3-level nested add → reorder → edit a nested block → delete a block with children → Cancel inside a nested modal →
save → reload: order, values, deletions and the cancel all hold; no orphan rows; front-end matches; restoring the
previous version restores blocks; previews update live before save; two block-list properties on one type both persist.
