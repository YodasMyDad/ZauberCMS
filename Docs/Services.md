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
  `HandlerResult<T>` (`Entity`, `Success`, `Messages`, `RefreshSignIn`). Add messages with
  `handlerResult.AddMessage(text, ResultMessageType.Error|Warning|Info|Success)`.
- Parameter conventions: `Cached`, `AsNoTracking = true`, collections `= []`, nullable filters, `OrderBy` enum,
  `PageIndex` (1-based) / `AmountPerPage`. Soft delete is inconsistent today: Get parameters and the Media/User
  Query parameters use `IncludeDeleted`; only `QueryContentParameters` uses `bool? IsDeleted = false` (`true` =
  recycle bin, `null` = both).
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
  → var key = query.GenerateCacheKey<T>()  // "TypeName-" + SHA256(ToQueryString()); append page for paged queries
  → parameters.Cached ? cacheService.GetSetCachedItemAsync(key, factory) : factory()
```

Reference: `ContentService.BuildQuery` / `QueryContentAsync`. Default cache TTL 240 min (`Extensions/CacheExtensions.cs`).
Cached results must be no-tracking and must not depend on user/culture unless that is part of the key.

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
7. Content saves set the original `DateUpdated` (optimistic concurrency) and handle `DbUpdateConcurrencyException`.

## Deletes

- Content/Media: `MoveToRecycleBin = true` → `Deleted = true`; otherwise hard delete (recycle-bin purge, block
  removal). Content hard delete also removes nested block-list descendants in a transaction.
- User: always soft delete + security-stamp reset.
- Everything else (ContentType, Domain, Role, Language, Tag, Redirect, ContentVersion, Audit): hard delete.
- Recycle-bin code paths must pass `IncludeDeleted = true` (Get, Media/User queries) / `IsDeleted = true` (`QueryContentParameters`).

## Audit

`SaveChangesAndLog` does **not** write audit rows ("Log" = error logging). Two patterns:

- `user.AddAudit(entity, name, AuditAction.X, auditService, cancellationToken)` (`Extensions/AuditExtensions.cs`) —
  Media, Tag, Seo, Language services.
- inline audit inside the save transaction — `ContentService`.

## AppState events

- Services raise: content deletes, media saves/deletes.
- Admin components raise after a successful service call: content saves, content-type and user events
  (`ContentEditor`, `ContentTypeEditor`, `UserEditor`, context menus).
- Don't raise the same event in both places. When adding a service-side notify, remove the component-side one.

## Validation

`ValidateService<T>` runs discovered `IValidate<T>` + FluentValidation validators; it is **UI-side** (Radzen
notifications) and called by editors before saving. Services do not call it — put invariants that must hold for every
caller in the service itself.
