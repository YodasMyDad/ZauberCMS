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
- Copy from `Data/Services/DataService.cs` (small) or `Seo/Services/SeoService.cs`. `Content/Services/ContentService*.cs`
  (one partial class, a file per concern: queries, save, delete, copy, routing, content types, domains,
  import/export, blocks) is the full reference (queries, versioning, block lists, transactions).
- Primary-constructor DI: `IServiceScopeFactory`, `ICacheService`, `ExtensionManager`, `ILogger<T>`, and as needed
  `AppState`, `IOptions<ZauberSettings>`, `ICurrentUserAccessor` (when it records who made a change), sibling
  services.
- `Components/_Imports.razor` globally injects the main services into every component (`ContentService`,
  `MediaService`, `MembershipService`, `LanguageService`, `TagService`, `SeoService`, `EmailService`, `DataService`).

## Method contract

- One parameter object + `CancellationToken cancellationToken = default`. No loose arguments on new methods.
- Returns: Get → `T?`; Query → `PaginatedList<T>` (`Shared/Models/PaginatedList.cs`); Save/Delete →
  `HandlerResult<T>` (`Entity`, `Success`, `Messages`, `RefreshSignIn` + `RefreshSignInTicket`). Add messages with
  `handlerResult.AddMessage(text, ResultMessageType.Error|Warning|Info|Success)`.
- Parameter conventions: `Cached`, `AsNoTracking = true`, collections `= []`, nullable filters, `OrderBy` enum,
  `PageIndex` (1-based) / `AmountPerPage`; the full list, `Cached` defaults included, is
  [Standards.md](Standards.md) §11. Soft delete (ZA-124): `IncludeDeleted` (also recycled items) on the Get and
  Query parameters of Content, Media and User and on `DataGridParameters`, plus `OnlyDeleted` (only recycled items)
  on `QueryContentParameters`; both default to `false`. The v4 `QueryContentParameters.IsDeleted` (`true` = only,
  `null` = both) is an `[Obsolete]` alias of them. Included `Children` never contain recycled items, whatever the filter.
  - `WhereClause` (expression) is always applied on top of standard filters.
  - `Query` (`Func<IQueryable<T>>`) **replaces** the whole query — including the soft-delete filter. Callers own it.

## Callers and access (Roadmap D27)

- **Services trust their caller.** They don't decide who may do what; whatever calls them does. Admin pages are
  gated by `[Authorize(Roles = Admin)]` ([AdminUI.md](AdminUI.md)); the account pages have their own checks
  (registration switch, emailed tokens, passwords, rate limits). A front-end component, controller, minimal API or
  plugin that calls a write method checks access itself first: `[Authorize]` on the endpoint, or comparing the
  signed-in user with the item an id from the address points at. Front-end code that writes on purpose (a sign-up
  form that creates content) needs nothing more. This is how Umbraco and WordPress do it too; a check inside every
  service was considered (D10, ZA-140) and dropped.
- **Who made a change.** Services read the caller through `ICurrentUserAccessor` (`Membership/Interfaces/`): for the
  audit trail, `LastUpdatedById`, a version's `CreatedById`, and refreshing the sign-in of a user who edits their own
  account. It answers, in order: a principal set by `RunAs`, the Blazor authentication state (a circuit or a static
  page), the request's user (a controller, a minimal API), nobody. Don't inject `AuthenticationStateProvider` into a
  service: it has a state only inside a Blazor render, and a save from anywhere else threw (ZA-093).
- **Nobody signed in** (a hosted service, a job, an import): the write is saved and records no user (null ids, no
  audit line). To record someone, run the calls as them:

```csharp
var principal = await signInManager.CreateUserPrincipalAsync(importUser);
using (currentUserAccessor.RunAs(principal))
{
    await contentService.SaveContentAsync(parameters, cancellationToken);
}
```

`RunAs` holds for the async flow that called it, not the DI scope (a circuit is one scope for all its events), so
call it in the method that awaits the work and dispose it there (`using`). It applies to the services of the scope
it was called in and to scopes with no Blazor render of their own (a job's); a circuit reached through an AppState
event keeps its own user. It is for jobs, imports and the installer, not components: work a render starts inside
the block keeps the value, and so do timers or loops first started there. The installer runs the starter kit as
the new administrator this way. Inside a circuit, use the services the component was given: a write service from a
fresh scope there falls back to the user the circuit connected with.
A site can register its own `ICurrentUserAccessor` after `AddZauberCms` to take the caller from somewhere else (an
API key's account, for example).

## DbContext per operation

```csharp
using var scope = serviceScopeFactory.CreateScope();
var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
```

Don't hold a context beyond the method. A scope, not `IDbContextFactory<T>` (Roadmap D59): the scope's
`IZauberDbContext` is the context of the provider the site uses now, chosen when the scope first asks (so the
database the installer has just set up is used without a restart, and a site with no database starts at all), which
a factory typed to one context class cannot do. Within one scope, `IZauberDbContext` and Identity's stores
(`UserManager`/`RoleManager` resolved from that scope) share one context (Phase 10, ZA-011), so a transaction
covers both. Resolve `UserManager` from the operation's scope, not by constructor injection into a service: a
circuit's one lives as long as the circuit and returns users as first loaded (ZA-086).

## Read path

```text
BuildQuery(parameters, dbContext)          // AsNoTracking, !Deleted / IncludeDeleted, filters, includes
  → if (parameters.Cached)                 // the key renders the SQL: build it only when caching
      key = query.GenerateCacheKey<T>()    // "TypeName-" + SHA256(SQL + Include calls); append page for paged queries
      cacheService.GetSetCachedItemAsync(key, factory)
    else factory()
```

Reference: `ContentService.BuildQuery` / `QueryContentAsync`. Default cache TTL 240 min (`Extensions/CacheExtensions.cs`).
Cached results must be no-tracking and must not depend on user/culture unless that is part of the key. Don't run a
query inside `BuildQuery` (filter by a joined column instead, as `ContentTypeAlias` does): it would run before the
cache check on every call. Page with `await query.ToPaginatedListAsync(pageIndex, size, cancellationToken)` (it skips the
`COUNT` when the first page isn't full); the sync `ToPaginatedList`, `PaginatedList(IQueryable…)` and `BuildPath` are
obsolete. No sync EF call (`FirstOrDefault`, `ToList`, `Any`…) inside an async method.

## Cache (Roadmap D13)

- `ICacheService` → `DefaultCacheService` (scoped) over `ServerCache` (singleton): values stay in this server's
  `IMemoryCache` as the objects that were loaded — never serialised — so navigations and role lists stay as loaded.
- Query keys hash the SQL **and** the query's `Include`/`ThenInclude`/`AsSplitQuery` calls (`ToQueryString` shows
  only the first statement of a split query). Build keys with `GenerateCacheKey`, not by hand.
- Prefix clears use an index of the keys this server cached (removed again on eviction). A value loaded while any
  clear ran is returned but not kept, so a save during a slow read can't leave the old value cached.
- Requests that miss the same key at the same moment share one load (`GetSetCachedItemAsync`; ZA-115), unless a
  clear ran since that load started: a request made after a save never gets a value read before it. A load whose
  request was cancelled is run again by the requests waiting for it. Don't ask for a key from inside its own load.
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
  publish — going through `ICacheService` would send them round again. The two defaults, `NoCacheInvalidationBus`
  (one server) and `RedisCacheInvalidationBus`, are public (sealed), so yours can wrap one.
- Writes inside a transaction: `SaveChangesAndLog` clears before the commit, so clear the prefix again after
  `CommitAsync` (a read in between may have cached the old rows) — see `ContentService` save, recycle and purge.
- `IDistributedCache` is still Redis when configured, for site code; ZauberCMS itself does not use it.
- Front-end hot path (Phase 10): the URL → page lookup (`GetContentFromRequestAsync`) is cached on slug, host and
  path — not the query string — under the Content prefix, at most 5 minutes (the key carries a 5-minute time
  window); a lookup that finds no page is not cached (`GetSetCachedItemAsync` with `shouldCache`). Domain, content
  type, role and language saves clear the Content prefix too: the cached page holds its type, roles and language. Its language keys and the default-language fallback are applied outside that cache, so dictionary
  and settings changes show at once. `GetBlocks` (without pending changes), `GetMedia`/`GetMediaItems` and
  `AnyContentAsync` (the 404 check; recycled content doesn't count) are cached as well; the items are shared, so
  don't change them. View, block-view and editor types are discovered once per app (`GetImplementations`, a static
  alias map in `RenderBlock`), not instantiated per request.

## Write path

1. Load the tracked entity (or create one: `Id = Guid.NewGuid().NewSequentialGuid()`).
2. `parameters.X.MapTo(entity)` — new properties must be added to `MapTo`.
3. Set `LastUpdatedById`, `DateUpdated = DateTime.UtcNow`.
4. `await dbContext.SaveChangesAndLog(entity, handlerResult, cacheService, extensionManager, cancellationToken)`
   saves every pending change on the context once, with the save hooks (Roadmap D28): `IBeforeEntitySave<T>` for
   every row the save writes, of every entity type (from the change tracker), each with its own state (added,
   modified, deleted) → one `SaveChanges` → the cache prefix of `T` and of every type written cleared →
   `IAfterEntitySave<T>` for the same rows (any `true` saves once more, without hooks) → `IAfterEntityCommit<T>` for
   the same rows once the change is committed (D62, step 8). The named entity's hooks run first, even when it has no
   changes. **One `false` from a before-save hook and nothing is written**: a move,
   recycle, restore, delete or emptied bin is one change. A concurrency conflict comes back as a user-readable
   error; other exceptions as `Success = false`.
5. Stage everything first, save once. Don't call `SaveChangesAsync` before it (an audit row saved early wrote the
   change too, so the hooks saw `Unchanged` and a veto could not stop it — ZA-012): add the audit row to the context
   and let the change's save write it. Clear related prefixes yourself
   (`cacheService.ClearCachedItemsWithPrefix(nameof(Related))`). Prefix match is `StartsWith`: `"Content"` also
   clears `ContentType-…`, `ContentVersion-…`.
6. Multi-entity writes: `await using var tx = await dbContext.Database.BeginTransactionAsync(cancellationToken)`, so
   a second save an after-save hook asks for, or two saves that must be in order (a version's old flag, then the new
   one), commit together. The `SaveChangesAndLog(entities, resultEntity, …)` overload names several entities.
7. Content saves set the original `DateUpdated` (optimistic concurrency).
8. Two after-hooks, as Umbraco has its "-ed" notifications after the scope completes (Roadmap D62):
   `IAfterEntitySave<T>` runs inside the caller's transaction, before the commit, and may ask for the second save;
   the change can still roll back after it (a media file that can't be removed, a later step that fails).
   `IAfterEntityCommit<T>` (`AfterCommitAsync`, async) runs only once the change is stored: right after the save
   when no transaction is open (after the second save too, and even if that one fails, since the first is stored),
   or when the caller's transaction (`Database.BeginTransaction`) commits; a rollback, a failed commit or a
   transaction disposed without a commit drops its calls. Use it for work outside the database (mirroring files,
   another system, an index, mail). It gets every row the change wrote, in the order saved, each with the state it
   was saved in; it cannot veto, and an exception it throws is logged (`Log.Error`, the hook's type) while the other
   hooks still run and the caller's result stays a success. The commit is seen by an EF Core transaction
   interceptor (`AfterCommitHooks`) that the three ZauberCMS DbContexts add in `OnConfiguring`; a site's own
   `IZauberDbContext` without it runs the hooks at the save instead, with one warning. An ambient
   `TransactionScope` is not seen: inside one the hooks run at the save. Bulk maintenance that runs straight
   against the database has no hooks: audit and version retention, orphan clean-up.

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
  `IAfterEntitySave<Media>` run for every item binned, restored or deleted, the items below the named one and an
  emptied bin's included; a veto on any of them stops the whole call (D28). `IAfterEntityCommit<Media>` hears of a
  permanent delete only when its files are gone and the rows are committed (D62). `GetMediaUsageAsync` finds the content and members whose property values contain the media's id
  (pickers, and the `media/{id}/` folder of a disk file) or URL: a scan of every property value, for a delete
  confirmation, never for a hot path; drafts are not searched.
  Recycled media is not served: `RestrictedMediaMiddleware` answers 404 to anyone but an administrator, unless a live
  public item has the same file, and marks every restricted or binned file it lets through `Cache-Control: private,
  no-store`. That covers files served by the site (`media/…`); a storage provider that serves files from its own
  address (a CDN, a bucket) has to keep binned files private itself, and a CDN in front of the site keeps the copies
  it took while the file was public until it is purged. The default `DiskStorageProvider` also removes the empty
  `media/{id}/` folder, and the image's resized copies (D63): ZauberCMS gives ImageResize's resizer a cache
  (`ResizedImageCache`) that keeps each image's copies in a folder named by a hash of its path
  (`{CacheRoot}/{shards}/{hash}/…`), so `DeleteFile` removes that folder; writing a file over one of the same name
  removes the old copies too. Binning keeps them (a restore brings the image back; a binned file is not served). Only
  the copies on the server that deletes are removed: a web farm with a cache per server keeps the others' (they are
  never served again through a resize URL, since the original is gone), as does a site that replaced
  `IImageResizerService`. A storage provider of a site's own removes whatever it derived from a file in its
  `DeleteFile`.
- Content URLs: one per page (not blocks; blocks have none). A typed URL another live page has is refused; a
  generated, copied or restored one gets a suffix. Stored form and request slugs go through `ContentUrl.Normalize`
  (lower case, no slashes around); links on the site use `ContentUrl.SiteRelative` (`/blog`).
- Drafts (`UnpublishedContent`): `SaveUnpublishedOnly` stores one; a save with property data publishes over it and
  removes it; a save with `ExcludePropertyData` (sort, move, restrict, culture, restore) keeps it and the stored
  `UnpublishedContentId`, whatever the caller's copy says. The editor shows a
  draft with `MapDraftTo`, so tree position, language and dates always come from the live row (a block's draft also
  carries its publish switch: `MapBlockDraftTo`). `ClearUnpublishedContentAsync` also drops the drafts of the page's
  blocks and deletes blocks only its draft used. A "Save only" runs the `UnpublishedContent` save hooks for the
  drafts it writes; the `Content` hooks see only the live rows it changes (the link to the draft, a new block's row)
  (D29). A restore that gives a page a new URL gives its draft that URL too.
- Tags: an item's tag links (`TagItem`, read by `QueryContentParameters.TagSlugs`) are written by the save that
  publishes its values (save, version restore, copy, import), in the same transaction, from the values of the
  properties whose editor has an `ITagReferenceProvider` (discovered; the built-in one reads `ZauberCMS.Tags`). A draft
  leaves them; a permanent delete removes them; a tag id that no longer exists is skipped. An item whose type has no
  tag property keeps links set through `ITagService.SaveTagItemAsync`. Property editors don't write to the database.
- Content type import over an existing alias keeps the ids of the tabs and properties it already has (matched by
  alias), so stored values stay attached. The type and the package's content are two saves: when the content save
  fails (a save hook's veto, a database error) the type stays imported without it, and the result is a failure.
- Versions: a draft version records the stored draft, and its blocks' drafts. Restoring a version
  (`PublishVersionAsync`) brings back name, navigation settings, property values and blocks only — not tree position,
  language or publish state — discards the pending drafts of the page and its blocks, deletes blocks the version does
  not have, and writes an audit row through `SaveChangesAndLog`. Retention (D25): the daily job keeps the newest
  `Zauber:ContentVersionsToKeep` versions of each page (default 50; 0 keeps all) plus its published version and
  latest draft (`PruneVersionsAsync`). `GetContentVersionsAsync` is not tracked and gives `CreatedBy` only id, name and
  email; `IncludeSnapshots = false` leaves the snapshots out (the admin list does).
- Public access: `SetContentRolesAsync` sets a page's roles on it and every page below it in one transaction (one
  audit row, the `ContentRole` save hooks, no versions) and is refused under an ancestor that already restricts
  access. Role queries (`GetRoleAsync`, `QueryRolesAsync`) load the role's members only with `IncludeUsers`.
- User (D35): `DeleteUserAsync` keeps the row, marked deleted, and erases the person in one `SaveChangesAndLog`:
  user name `deleted-<id>`, email, phone and password cleared, profile values, logins, tokens, claims and roles
  removed, a new security stamp (sessions end). The address and the old name can register again; there is no
  restore. Audit rows keep the text they were written with (the audit row of the delete is written after the save).
  Refused: deleting your own account, and the last administrator. Deleting is not a ban: the person can register or
  sign in through a provider again; to keep someone out, lock the account.
- Redirects (D34): `SaveRedirectAsync` refuses a From URL that is empty, an exact one that does not start with `/`,
  a regex that does not parse, and a redirect that sends an address to itself or closes a circle with the saved ones
  (`RedirectRules.FindLoop`). `SeoRedirect.IsRegex` null (rows from before the flag) keeps the old guess
  (`RedirectRules.LooksLikeRegex`). `RedirectMiddleware` keeps its compiled list in `ICacheService` under the
  `SeoRedirect` prefix, so a save clears it on every server; exact matches ignore letter case, and no rule sends an
  address to that same address. A redirect with a domain (`SeoRedirect.DomainId`, D42) applies only to requests on
  that domain, by the rule content routing uses (`DomainMatching`: same host, the domain's path on whole segments;
  scheme, trailing dot and slashes ignored; a domain with a port matches only that port). A request is on one domain
  at most, the longest that matches, as for content (ZA-169), so a redirect for `example.com` does not fire under
  `example.com/fr` when that is a domain. One without a domain applies on every host; for the same From URL the one
  for the request's domain wins, and exact rules are still tried before regular expressions. The From URL is the
  whole path, also for a domain with a path (`example.com/fr` → From URL `/fr/old`). The loop check follows the chain
  per host as requests would, working out the domain at each step (`RedirectRules.FindLoop` with the site's domains).
  Deleting a domain deletes its redirects in the same change, as tracked deletes with an audit row each, so the
  save hooks see them, one veto keeps the domain and all of them, and the `SeoRedirect` cache is cleared (ZA-193;
  the database cascade is only a backstop).
- Everything else (ContentType, Domain, Role, Language, Tag, Redirect, ContentVersion, Audit): hard delete.
- Recycle-bin code paths must pass `IncludeDeleted = true` (Get, queries) or `OnlyDeleted = true` (`QueryContentParameters`, bin only).
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

Both name the caller from `ICurrentUserAccessor`; with nobody signed in no audit row is written.

Audit rows older than 90 days are removed by the daily job (`CleanupOldAuditsAsync`, one `DELETE` in the database).

## Background jobs (Roadmap D61)

ZauberCMS runs two hosted jobs, each with a switch under `Zauber:Jobs` (on by default; read when the site starts,
`IOptions<ZauberSettings>`, so `Configure<ZauberSettings>` in code works too). A job that is off logs one line at
start-up and does nothing else.

| Key | Job | What it does |
| --- | --- | --- |
| `Zauber:Jobs:DailyCleanup` | `DailyJob` (Core) | First run five minutes after start-up, then daily: audit rows older than 90 days, orphaned related-content links, page versions beyond `ContentVersionsToKeep`. Writes to the shared database. |
| `Zauber:Jobs:SitemapGenerator` | `SitemapGeneratorService` (Components) | At start-up, then every six hours: writes each sitemap set up in the admin to an XML file in this server's web root. |

Several servers on one database: keep `DailyCleanup` on one server and turn it off on the others (like Umbraco's
scheduled jobs, which run on its "scheduling publisher" server only). Keep `SitemapGenerator` on wherever the
sitemap file is served from: on each server, or on one when the web root is shared. The Redis cache-clear listener
(`ICacheInvalidationBus`) is not a job and runs on every server. Both jobs do nothing while the site has no database.

## AppState events

- Services raise: content deletes, media saves/deletes/restores (after the save succeeded, once — ZA-013).
- Admin components raise after a successful service call: content saves, content-type and user events
  (`ContentEditor`, `ContentTypeEditor`, `UserEditor`, context menus).
- Don't raise the same event in both places. When adding a service-side notify, remove the component-side one.

## Validation

`ValidateService<T>` runs discovered `IValidate<T>` + FluentValidation validators; it is **UI-side** (Radzen
notifications, so it is in ZauberCMS.Components with its Core namespace, Roadmap D51) and called by editors before saving. Services do not call it — put invariants that must hold for every
caller in the service itself.
