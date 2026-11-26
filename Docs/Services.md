# Services

How Core services are built. Paths relative to `ZauberCMS.Core/`. Standards: [Standards.md](Standards.md).

## Shape

```text
<Feature>/Interfaces/IXService.cs
<Feature>/Services/XService.cs
<Feature>/Parameters/{Get|Query|Save|Delete}XParameters.cs
<Feature>/Mapping/XMappingExtensions.cs   // MapTo(), hand-written
<Feature>/Mapping/XDbMapping.cs           // EF IEntityTypeConfiguration
```

- Register `AddScoped<IXService, XService>()` in `ZauberSetup.cs` next to the others.
- Copy from `Data/Services/DataService.cs` (small) or `Seo/Services/SeoService.cs`. `Content/Services/ContentService.cs`
  is the full reference (queries, versioning, block lists, transactions).
- Primary-constructor DI: `IServiceScopeFactory`, `ICacheService`, `ExtensionManager`, `ILogger<T>`, and as needed
  `AppState`, `IOptions<ZauberSettings>`, sibling services.
- `Components/_Imports.razor` globally injects the main services into every component (`ContentService`,
  `MediaService`, `MembershipService`, `LanguageService`, `TagService`, `SeoService`, `EmailService`, `DataService`).

## Method contract

- One parameter object + `CancellationToken cancellationToken = default`. No loose arguments on new methods.
- Returns: Get → `T?`; Query → `PaginatedList<T>` (`Shared/Models/PaginatedList.cs`); Save/Delete →
  `HandlerResult<T>` (`Entity`, `Success`, `Messages`, `RefreshSignIn` + `RefreshSignInTicket`). Add messages with
  `handlerResult.AddMessage(text, ResultMessageType.Error|Warning|Info|Success)`.
- Parameter conventions: `Cached`, `AsNoTracking = true`, collections `= []`, nullable filters, `OrderBy` enum,
  `PageIndex` (1-based) / `AmountPerPage`. Soft delete is inconsistent today: Get parameters and the Media/User
  Query parameters use `IncludeDeleted`; only `QueryContentParameters` uses `bool? IsDeleted = false` (`true` =
  recycle bin, `null` = both). Included `Children` never contain recycled items, whatever the filter.
  - `WhereClause` (expression) is always applied on top of standard filters.
  - `Query` (`Func<IQueryable<T>>`) **replaces** the whole query — including the soft-delete filter. Callers own it.

## DbContext per operation

```csharp
using var scope = serviceScopeFactory.CreateScope();
var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
```

Don't hold a context beyond the method. Identity (`UserManager`/`RoleManager`) uses a **different** context instance
from `IZauberDbContext` (see Standards → known deviations): don't expect one `SaveChanges` to commit both.

## Read path

```text
BuildQuery(parameters, dbContext)          // AsNoTracking, !Deleted / IncludeDeleted, filters, includes
  → var key = query.GenerateCacheKey<T>()  // "TypeName-" + SHA256(SQL + Include calls); append page for paged queries
  → parameters.Cached ? cacheService.GetSetCachedItemAsync(key, factory) : factory()
```

Reference: `ContentService.BuildQuery` / `QueryContentAsync`. Default cache TTL 240 min (`Extensions/CacheExtensions.cs`).
Cached results must be no-tracking and must not depend on user/culture unless that is part of the key.

## Cache (Roadmap D13)

- `ICacheService` → `DefaultCacheService` (scoped) over `ServerCache` (singleton): values stay in this server's
  `IMemoryCache` as the objects that were loaded — never serialised — so navigations and role lists stay as loaded.
- Query keys hash the SQL **and** the query's `Include`/`ThenInclude`/`AsSplitQuery` calls (`ToQueryString` shows
  only the first statement of a split query). Build keys with `GenerateCacheKey`, not by hand.
- Prefix clears use an index of the keys this server cached (removed again on eviction). A value loaded while any
  clear ran is returned but not kept, so a save during a slow read can't leave the old value cached.
- Several servers: set `Zauber:RedisConnectionString`. Redis carries **only invalidations**
  (`ICacheInvalidationBus` → `RedisCacheInvalidationBus`): each clear increments
  `zauber:<ApplicationName>:cache:counter` and publishes on `zauber:<ApplicationName>:cache` in one script. A server
  that sees a gap in the counter, is behind it on two checks 30 s apart, sees it go backwards (Redis lost it), or
  (re)connects to Redis drops everything it cached; clears that could not be sent go out as one "clear all" once
  Redis is reachable. Nothing cached is
  stored in Redis. Sites that share one Redis should have different application names (otherwise they clear each
  other's caches: harmless, just wasted work).
- Another backplane: register your own `ICacheInvalidationBus` singleton after `AddZauberCms` (the last registration
  wins). Apply the clears it receives with `ServerCache.Remove` / `RemoveWithPrefix` / `RemoveAll`, which don't
  publish — going through `ICacheService` would send them round again.
- Writes inside a transaction: `SaveChangesAndLog` clears before the commit, so clear the prefix again after
  `CommitAsync` (a read in between may have cached the old rows) — see `ContentService` save, recycle and purge.
- `IDistributedCache` is still Redis when configured, for site code; ZauberCMS itself does not use it.

## Write path

1. Load the tracked entity (or create one: `Id = Guid.NewGuid().NewSequentialGuid()`).
2. `parameters.X.MapTo(entity)` — new properties must be added to `MapTo`.
3. Set `LastUpdatedById`, `DateUpdated = DateTime.UtcNow`.
4. `await dbContext.SaveChangesAndLog(entity, handlerResult, cacheService, extensionManager, cancellationToken)`:
   runs `IBeforeEntitySave<T>` (can veto) → saves → clears cache prefix `typeof(T).Name` → runs `IAfterEntitySave<T>`.
   It catches exceptions into `Success = false`.
5. Clear related prefixes yourself (`cacheService.ClearCachedItemsWithPrefix(nameof(Related))`) — e.g. tags and
   languages do this. Prefix match is `StartsWith`: `"Content"` also clears `ContentType-…`, `ContentVersion-…`.
6. Multi-entity writes: `await using var tx = await dbContext.Database.BeginTransactionAsync(cancellationToken)`.
   The `SaveChangesAndLog(entities, resultEntity, …)` overload saves once and runs the hooks for each listed entity
   with its own state (added, modified, deleted); a concurrency conflict comes back as a user-readable error.
7. Content saves set the original `DateUpdated` (optimistic concurrency) and handle `DbUpdateConcurrencyException`.

## Deletes

- Content/Media: `MoveToRecycleBin = true` (the Media default) → `Deleted = true`; otherwise hard delete
  (recycle-bin purge). Content hard delete also removes the blocks of every deleted item (those its draft reaches
  too) in the same transaction, and notifies with the deleted item. Blocks are found through
  `INestedContentReferenceProvider` (→ [BlockListEditor.md](BlockListEditor.md) §2).
- `SaveContentAsync` saves an item's values together with its blocks (`SaveContentParameters.Blocks` = the blocks
  added or edited; the admin editor passes its edits, other code usually none): one transaction, blocks no longer
  reached deleted with everything below them, the blocks' pending drafts published, one audit row and one version
  after the blocks are written; with `SaveUnpublishedOnly` the item and its edited blocks get drafts and the live
  site stays as it is. It raises no events. A copy opened before someone else's "Save only" is refused. A save with
  `ExcludePropertyData` (sort, move, restore, restrict, culture) leaves values and blocks alone.
- Content and its tree (Phase 5): recycling a page recycles every page below it, all stamped with one `DateUpdated`;
  a save that sets `Deleted = false` (the bin's Restore) restores the pages with that stamp — a page binned on its
  own earlier stays in the bin — and gives a page whose URL was taken meanwhile a `-1` suffix (warning message).
  Restoring a page whose parent is still in the bin is refused.
  Permanent delete takes the recycled pages below with it and is refused while any page below is live. Descendants
  are found by walking `ParentId` (`GetDescendantsAsync`), not by the stored `Path`. A save that changes the parent
  rewrites the `Path` of everything below and refuses a move under the page's own descendant.
- Media and its tree (Phase 8, D3) work the same way, with their own methods instead of a save:
  `DeleteMediaAsync` bins an item with everything below it (one `DateUpdated` stamp); `RestoreMediaAsync` brings back
  that group and is refused while the parent folder is in the bin. `SaveMediaAsync` never changes `Deleted`, refuses
  to change an item that is in the bin (that would move its stamp off the group) and refuses to put anything into a
  binned folder; included `Children` never contain binned items. `DeleteMediaAsync` with `MoveToRecycleBin = false`
  deletes the rows (refused while anything below is live) and the files (`DeleteFile`, default true) together: the
  files go before the commit, and one that can't be removed rolls the rows back, because a file left without its row
  is served to anyone. A file another media row still points at is kept. `EmptyMediaRecycleBinAsync` does the same
  for the whole bin and keeps a binned folder that still holds live media. `IBeforeEntitySave<Media>` /
  `IAfterEntitySave<Media>` run for the item the call names, not for the items below it, and not at all when the
  bin is emptied. `GetMediaUsageAsync` finds the content and members whose property values contain the media's id
  (pickers, and the `media/{id}/` folder of a disk file) or URL: a scan of every property value, for a delete
  confirmation, never for a hot path; drafts are not searched.
  Recycled media is not served: `RestrictedMediaMiddleware` answers 404 to anyone but an administrator, unless a live
  public item has the same file, and marks every restricted or binned file it lets through `Cache-Control: private,
  no-store`. That covers files served by the site (`media/…`); a storage provider that serves files from its own
  address (a CDN, a bucket) has to keep binned files private itself, and a CDN in front of the site keeps the copies
  it took while the file was public until it is purged. Resized copies in `_mediacache` are not removed by a delete.
  The default `DiskStorageProvider` also removes the empty `media/{id}/` folder.
- Content URLs: one per page (not blocks; blocks have none). A typed URL another live page has is refused; a
  generated, copied or restored one gets a suffix. Stored form and request slugs go through `ContentUrl.Normalize`
  (lower case, no slashes around); links on the site use `ContentUrl.SiteRelative` (`/blog`).
- Drafts (`UnpublishedContent`): `SaveUnpublishedOnly` stores one; a save with property data publishes over it and
  removes it; a save with `ExcludePropertyData` (sort, move, restrict, culture, restore) keeps it and the stored
  `UnpublishedContentId`, whatever the caller's copy says. The editor shows a
  draft with `MapDraftTo`, so tree position, language and dates always come from the live row (a block's draft also
  carries its publish switch: `MapBlockDraftTo`). `ClearUnpublishedContentAsync` also drops the drafts of the page's
  blocks and deletes blocks only its draft used.
- Versions: a draft version records the stored draft, and its blocks' drafts. Restoring a version
  (`PublishVersionAsync`) brings back name, navigation settings, property values and blocks only — not tree position,
  language or publish state — discards the pending drafts of the page and its blocks, deletes blocks the version does
  not have, and writes an audit row through `SaveChangesAndLog`.
- User: always soft delete + security-stamp reset.
- Everything else (ContentType, Domain, Role, Language, Tag, Redirect, ContentVersion, Audit): hard delete.
- Recycle-bin code paths must pass `IncludeDeleted = true` (Get, Media/User queries) / `IsDeleted = true` (`QueryContentParameters`).
  That includes the admin editor that opens an item from the bin (`EditContent`, `ContentEditor`).
- Grid methods (`GetDataGridAsync<T>`, `GetDataGridContentAsync`, `GetDataGridLanguageDictionaryAsync`) take the
  grid's filter and sort strings. `GridQueryGuard` parses and checks them: entity properties and simple value
  methods only, never a secret column; anything else throws `ArgumentException`.
- When `RefreshSignIn` is true the signed-in user changed their own account: navigate (full page load) to
  `Urls.ApiRefreshSignIn?redirectUrl=<local path>&ticket=<RefreshSignInTicket>`. Without the ticket the endpoint
  only renews a cookie whose security stamp is still valid.
- `IDataService.GetDataGridAsync<T>` (dashboard grids) leaves out deleted Content/Media/User rows unless
  `DataGridParameters.IncludeDeleted` is set.
- Content saves are checked against `DateUpdated`: a loaded copy can be saved **once**; a second save of the same
  object is refused as an edit of an outdated copy. A dialog that saves must be the only one that saves — the code
  that opened it uses the saved entity from the dialog result and does not save again.

## Audit

`SaveChangesAndLog` does **not** write audit rows ("Log" = error logging). Two patterns:

- `user.AddAudit(entity, name, AuditAction.X, auditService, cancellationToken)` (`Extensions/AuditExtensions.cs`) —
  Media, Tag, Seo, Language services.
- inline audit inside the save transaction — `ContentService`.

## AppState events

- Services raise: content deletes, media saves/deletes/restores (after the save succeeded, once — ZA-013).
- Admin components raise after a successful service call: content saves, content-type and user events
  (`ContentEditor`, `ContentTypeEditor`, `UserEditor`, context menus).
- Don't raise the same event in both places. When adding a service-side notify, remove the component-side one.

## Validation

`ValidateService<T>` runs discovered `IValidate<T>` + FluentValidation validators; it is **UI-side** (Radzen
notifications) and called by editors before saving. Services do not call it — put invariants that must hold for every
caller in the service itself.
