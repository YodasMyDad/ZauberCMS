# Audit: Memory Leaks & Object Lifetime

## Your task

Read-only audit of the ZauberCMS codebase for memory leaks, lifetime
mismatches, and disposal bugs. Produce a structured findings report. **Do not
edit any code.** Do not run the application.

## Repo context you need

- Long-lived processes: this is an ASP.NET Core / Blazor Server app — leaks
  accumulate across requests and SignalR circuits.
- `AppState` (`ZauberCMS.Core/Shared/AppState.cs`) is a **singleton** using
  `WeakEventManager<T>`. Subscribers are weakly held, but missed unsubscribes
  can still cause silent "events stopped firing" issues. List both leak risks
  AND missed-unsubscribe risks.
- Admin UI (`/admin`) is **InteractiveServer** Blazor. Components live for the
  duration of a SignalR circuit; components subscribing to events must
  unsubscribe in `Dispose` / `DisposeAsync`.
- Front-end is **static SSR** — no per-circuit state, but components rendered
  in SSR still need disposal discipline if they hold IDisposables.
- Services are typically scoped or singleton; singletons that need scoped
  dependencies use `IServiceScopeFactory`.
- Three EF `DbContext` types in `ZauberCMS.Core/Data/`. DbContexts must not
  outlive their scope.
- Cache: `ICacheService` / `DefaultCacheService.cs` — invalidation is by
  prefix, not exact key. Long-running caches with unbounded keys are a leak.

## What to flag

For each finding, classify by severity (Critical / High / Medium / Low) and
confidence (High / Medium / Low).

### Event subscription leaks
- Razor components in `ZauberCMS.Components/` subscribing to `AppState` events
  (`OnContentChanged`, `OnUserChanged`, `OnMediaChanged`, etc.) in
  `OnInitialized` / `OnInitializedAsync` without unsubscribing in `Dispose`.
- Same pattern with .NET events (`+=`) anywhere — every `+=` must have a
  matching `-=` in disposal.
- Subscriptions to `NavigationManager.LocationChanged` without unsubscribe.
- `EventCallback`s captured in long-lived state.

### IDisposable discipline
- `IDisposable` / `IAsyncDisposable` instances created with `new` and not
  inside a `using` / `await using`. Common offenders:
  `HttpClient` (should be `IHttpClientFactory` anyway), `FileStream`,
  `StreamReader/Writer`, `SemaphoreSlim`, `CancellationTokenSource`,
  `DbContext` (when manually instantiated).
- Classes that hold `IDisposable` fields but don't implement `IDisposable`.
- `DbContext` instances created from `IServiceScopeFactory` where the **scope**
  is not disposed (a `using` on the context alone isn't enough).

### Blazor component disposal
- Components in `ZauberCMS.Components/Admin/**` that hold timers, subscriptions,
  `CancellationTokenSource`, or background tasks but don't implement
  `IDisposable` / `IAsyncDisposable`.
- `Timer` / `System.Threading.Timer` not disposed.
- `PeriodicTimer` not disposed.
- `CancellationTokenSource` not disposed (especially common around
  `StateHasChanged` debounce patterns).

### Singleton / scoped lifetime mismatch
- Singletons that **capture** scoped services (rather than resolving them
  per-call via `IServiceScopeFactory.CreateScope()`) — the scoped instance
  becomes effectively a singleton and may hold references to disposed objects.
- Static fields holding scoped objects.
- Closures captured by long-lived delegates that pin scoped instances.

### Unbounded growth
- Static `Dictionary<,>` / `List<>` / `ConcurrentDictionary` that only ever
  grow (no eviction, no upper bound).
- Caches with user-controlled keys and no size limit.
- Logging/buffering that keeps references alive (e.g. in-memory log buffers).
- Reflection result caching keyed by something with infinite cardinality.

### EF tracking leaks
- Long-lived `DbContext` (held longer than a single unit of work) that keeps
  tracking entities — change tracker grows.
- `Find` / `Single` / `First` on a context, then the same context reused for
  unrelated work without `ChangeTracker.Clear()` or scope disposal.

### Cache key explosion
- Cache keys built from user input, request paths, query strings, or other
  effectively-unbounded sources, written into `IMemoryCache` without a size
  limit (`MemoryCacheEntryOptions.Size` + `MemoryCacheOptions.SizeLimit`).
- Prefix invalidation patterns where the prefix is so coarse it never matches,
  letting old entries linger forever.

### Static event leaks
- `static event` declarations — every subscriber is rooted forever.
- `EventHandler<>` patterns on singleton services where the singleton outlives
  any reasonable subscriber lifetime.

## What NOT to flag

- `WeakEventManager<T>` itself (chosen primitive — see CLAUDE.md note).
- Singletons that hold other singletons (no lifetime mismatch).
- `IMemoryCache` / `ICacheService` registration (it's the right abstraction).

## Workflow

This is a **find-and-fix** audit. Two phases:

### Phase 1 — Audit
Walk the codebase, identify findings, and write them to
`.audit-reports/02-memory.md` (structure below). Order by severity then
confidence.

### Phase 2 — Fix
Apply fixes for findings that meet ALL of these criteria:
- Severity is **Critical** or **High**, OR Medium with High confidence.
- The fix is mechanical: add a missing `-=` unsubscribe, implement
  `IDisposable`/`IAsyncDisposable`, dispose a `CancellationTokenSource`,
  wrap something in `using`, dispose a scope, fix a singleton-captures-scoped
  pattern by using `IServiceScopeFactory`.
- The fix doesn't require redesign (e.g. switching cache providers, moving
  to bounded caches with eviction policy — that's design, skip it).
- You're confident the fix doesn't break callers.

**Skip fixing** (leave in the report, marked `Status: not fixed`) when:
- Severity is Low.
- Confidence is Low.
- The fix requires architectural decisions (cache eviction strategy,
  swapping primitives, new lifetime model).
- The fix would touch >10 files.

After all fixes:
- Run `dotnet build ZauberCMS.sln -c Release` and confirm it succeeds.
- If the build fails, fix the build error, build again, repeat.
- Do **not** run migrations or run the app.

## Output

Write a single file: `.audit-reports/02-memory.md`

```markdown
# Memory & Lifetime Audit

_Generated: <UTC timestamp>_
_Build status after fixes: PASSED / FAILED_

## Summary
| | Found | Fixed | Skipped |
|--|--|--|--|
| Critical | N | N | N |
| High | N | N | N |
| Medium | N | N | N |
| Low | N | N | N |

## Findings

### [SEVERITY] Short title
- **File:** `path/to/file.cs:line`
- **Confidence:** High / Medium / Low
- **Status:** FIXED / not fixed
- **Issue:** One paragraph.
- **Trigger & rate:** per-request / per-circuit / per-startup, plus rough
  rate.
- **Why it matters:** Concrete impact.
- **Fix applied:** What you changed. Omit if not fixed.
- **Why not fixed:** Reason. Omit if fixed.
- **Notes:** Anything you weren't sure about.

(repeat per finding)

## Files modified
List every file you edited with a one-line summary.

## Areas you did not fully cover
List any directories / files you didn't get to and why.
```

If a category has no findings, write "No issues found."

## Hard rules

- Edits allowed only for findings that pass the Phase 2 criteria.
- Do not introduce new abstractions or dependencies.
- Do not refactor surrounding code outside the finding's scope.
- `dotnet build` only — no app run, no migrations.
- If a fix breaks the build and you can't repair it in scope, **revert** and
  mark `Status: not fixed — build broke, reverted`.
- Cite file paths and line numbers for every finding.
