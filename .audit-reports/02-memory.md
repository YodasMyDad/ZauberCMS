# Memory & Lifetime Audit

_Generated: 2026-05-07T15:55Z_
_Build status after fixes: PASSED_

## Summary
| | Found | Fixed | Skipped |
|--|--|--|--|
| Critical | 0 | 0 | 0 |
| High | 1 | 1 | 0 |
| Medium | 2 | 0 | 2 |
| Low | 2 | 0 | 2 |

## Findings

### [HIGH] ContentEditor subscribes to AppState.OnContentChanged inside OnParametersSetAsync
- **File:** `ZauberCMS.Components/Admin/ContentSection/ContentEditor.razor:310-315`
- **Confidence:** High
- **Status:** FIXED
- **Issue:** `OnParametersSetAsync` runs on every parameter change, but it contains
  `AppState.OnContentChanged += HandleContentChanged;`. Each cascade of parameter
  changes appends another live subscription. `Dispose` only ever does one `-=`,
  but `WeakEventManager.RemoveHandler` uses `_handlers.RemoveAll` so the cleanup
  itself is fine — the real damage is during the component's lifetime: the
  weak-ref list grows linearly and `HandleContentChanged` is invoked N times for
  every event. With N parameter sets per visit, every save inside the admin
  fires N redundant refreshes per editor instance, magnifying DB load and
  potentially racing each other.
- **Trigger & rate:** per-circuit, per ContentEditor instance, per parameter
  change. In a typical admin session, parameters change every navigation.
- **Why it matters:** Both a duplicate-event bug (visible to users as repeated
  refreshes/spinner flicker) and a transient memory cost (weak-ref list grows
  for the life of the component before being pruned). Other tree/editor
  components correctly subscribe in `OnInitialized` / `OnInitializedAsync`; this
  one was the outlier.
- **Fix applied:** Added a dedicated `protected override void OnInitialized()`
  that performs the subscription once, and removed the `+=` from
  `OnParametersSetAsync`. The matching `-=` in `Dispose` is unchanged.

### [MEDIUM] DefaultCacheService.Keys grows unbounded under Redis backend
- **File:** `ZauberCMS.Core/Shared/Services/DefaultCacheService.cs:15,32-77,84-129`
- **Confidence:** High
- **Status:** not fixed
- **Issue:** A `static readonly ConcurrentDictionary<string, byte> Keys` tracks
  every cache key written through this service so prefix-invalidation
  (`ClearCachedItemsWithPrefix`) can iterate them. On the in-memory path the
  `RegisterPostEvictionCallback` removes the key when the entry is evicted, so
  growth is self-bounding. On the **Redis path** (lines 51-52, 103-104) there
  is no equivalent eviction hook, so `Keys` only ever loses entries when the
  app explicitly calls `ClearCachedItem` / `ClearCachedItemsWithPrefix`. Cache
  keys are SHA256-hashed query strings — bounded by query cardinality, but
  query cardinality grows with content/user/parameter combinations, and the
  key entry itself is small but never released for the lifetime of the
  process.
- **Trigger & rate:** per cache miss in Redis mode. Keys accumulate at the rate
  of distinct queries seen during the process lifetime.
- **Why it matters:** Long-running processes with Redis caching slowly
  accumulate keys in process memory. Each is short (~50 chars + dict overhead)
  so it's not catastrophic, but the dictionary pinpoints exactly the kind of
  silent growth that's hard to notice until restart cadence drops.
- **Why not fixed:** A correct fix needs an architectural decision — either
  Redis pub/sub for eviction notifications, periodic resync via SCAN, or
  switching the prefix-invalidation strategy to use Redis-side keyspace
  iteration. None of these are mechanical edits.
- **Notes:** Also note that `IMemoryCache` is registered implicitly (no
  `MemoryCacheOptions.SizeLimit` configured anywhere), but for the in-memory
  path the post-eviction callback at least keeps `Keys` bounded by the active
  cache. The unbounded path is specifically the Redis branch.

### [MEDIUM] EditContextFluentValidationExtensions never unsubscribes from EditContext events
- **File:** `ZauberCMS.Core/Shared/Validation/EditContextFluentValidationExtensions.cs:22-27`
- **Confidence:** Medium
- **Status:** not fixed
- **Issue:** `AddFluentValidation` does
  `editContext.OnValidationRequested += ...` and
  `editContext.OnFieldChanged += ...` but never removes them. The lambdas
  capture `serviceProvider`, the optional `validator`, the
  `fluentValidationValidator` reference, and a per-call
  `ValidationMessageStore`. Those captures are rooted by the EditContext for
  the lifetime of the EditContext.
- **Trigger & rate:** per form (per call to `AddFluentValidation`). Practically
  bounded by the lifetime of the EditContext, which itself is per-form /
  per-component.
- **Why it matters:** In Blazor InteractiveServer, an EditContext lives for as
  long as the component holding the form does. If the same component reuses an
  EditContext across multiple `AddFluentValidation` calls (e.g. binding model
  changes in long-lived editors), every call piles on another subscription,
  and each retains its captured `serviceProvider`. In typical usage this is
  bounded, but it's a brittle pattern.
- **Why not fixed:** The `AddFluentValidation` method is an extension with no
  natural place to expose a disposal hook back to callers. The fix needs an
  `IDisposable` token returned by `AddFluentValidation` (or moving the +=
  inside a wrapper component that owns the lifecycle). Either is a small
  redesign rather than a mechanical change. Pattern was inherited from a
  Steve Sanderson reference implementation per the file comments.
- **Notes:** This is the only place in the codebase that subscribes to
  `EditContext` events without unsubscribing. All `AppState.On*` /
  `TreeState.OnTreeValueChanged` subscriptions in razor components do
  unsubscribe correctly in `Dispose`.

### [LOW] SortableList does not dispose its imported JS module
- **File:** `ZauberCMS.Components/Shared/SortableList.razor:66-67,85`
- **Confidence:** High
- **Status:** not fixed
- **Issue:** `var module = await JS.InvokeAsync<IJSObjectReference>("import", ...)`
  is created during first render but never stored or disposed. The .NET wrapper
  is GC'd, but the underlying JS-side reference is tracked by the Blazor JS
  runtime for the lifetime of the SignalR circuit. `selfReference`
  (a `DotNetObjectReference`) is correctly disposed in `Dispose()`.
- **Trigger & rate:** per SortableList instance, per circuit.
- **Why it matters:** Each circuit accumulates one orphan JS reference per
  SortableList that ever rendered. Bounded by circuit lifetime; not a
  long-running-process leak. Best-practice fix is to store `module` as a field
  and `await using`-style dispose it in `DisposeAsync`.
- **Why not fixed:** Requires switching the component from `IDisposable` to
  `IAsyncDisposable` (or adding both), which is a small refactor that could
  ripple to consumers expecting a sync disposable. Marked Low and left for
  follow-up.

### [LOW] DefaultCacheService static `Keys` dictionary is process-wide rather than service-wide
- **File:** `ZauberCMS.Core/Shared/Services/DefaultCacheService.cs:15`
- **Confidence:** Medium
- **Status:** not fixed
- **Issue:** `Keys` is `static`, but `DefaultCacheService` is registered as
  Scoped. There is no actual leak because the dictionary backs the only
  prefix-invalidation primitive the codebase has, and it must outlive any
  individual scope. Flagged because static collections in scoped services are
  worth recording.
- **Trigger & rate:** N/A (per-startup field).
- **Why it matters:** Mostly clarity — anyone reading the code might assume
  per-scope behaviour given the registration. Combined with the Medium finding
  above, this is the same dictionary that grows unbounded in Redis mode.
- **Why not fixed:** Intentional design.

## Files modified
- `ZauberCMS.Components/Admin/ContentSection/ContentEditor.razor` — added
  `OnInitialized` to subscribe to `AppState.OnContentChanged` once; removed the
  `+=` from `OnParametersSetAsync`.

## Areas you did not fully cover
- Did not exhaustively walk every `.razor` component in `ZauberCMS.Web/Pages/`
  and `ZauberCMS.Web/ContentBlocks/` — only spot-checked. These are SSR-only
  consumer-side templates and contain no event subscriptions or IDisposable
  resources by inspection (`+=` / `new Timer/CTS/HttpClient` / `DotNetObjectReference`
  greps returned nothing on this tree).
- Did not analyse the third-party `ImageResize`, `Radzen`, `BlazoredModal`,
  `TinyMCE`, or `ZauberCMS.RTE` packages — out of scope.
- Did not inspect `bin`/`obj` artifacts.
