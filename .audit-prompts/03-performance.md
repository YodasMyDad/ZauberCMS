# Audit: Performance Hotspots

## Your task

Read-only audit of the ZauberCMS codebase for performance issues — query
inefficiency, unnecessary work in hot paths, cache misuse, and reflection
overhead. Produce a structured findings report. **Do not edit any code.**
Do not run the application or load tests.

## Repo context you need

- This is a CMS — request paths matter most. Hot paths:
  1. `EntryPage.razor` (`ZauberCMS.Components/Pages/EntryPage.razor`) — every
     front-end request. Note: `CultureMiddleware.GetEntryModel(HttpContext)` is
     called first to **avoid a double DB hit**; if `EntryPage` ever falls back
     to `ContentService.GetContentFromRequestAsync()`, that's the second hit.
  2. `ContentService` (`ZauberCMS.Core/Content/Services/ContentService.cs`)
     — content lookup, GetValue, block list resolution.
  3. `CatchAll.razor` (`ZauberCMS.Routing/CatchAll.razor`) — every front-end
     request that doesn't match a more specific route.
  4. `RestrictedMediaMiddleware` and image-resize middleware (registered in
     `ZauberSetup.cs`).
- Three EF providers (SQL Server / SQLite / PostgreSQL) — query patterns must
  work efficiently on all three. Provider-specific quirks are fair game.
- Caching: `ICacheService` (`DefaultCacheService.cs`). Pattern is
  `query.GenerateCacheKey()` → `GetSetCachedItemAsync(...)`. Invalidation by
  **prefix**.
- Extension discovery: `ExtensionManager` reflects over assemblies registered
  by `AssemblyManager`. Reflection results should be cached — flag any per-
  request reflection.
- Identity tables prefixed `Zauber*`. GUIDs are sequential
  (`NewSequentialGuid()`), so PK ordering is not random.
- Soft-delete only — every list query must filter `Deleted == false` (or
  the inverse for recycle bin). Missing filters cause both correctness AND
  perf issues (unbounded result sets).

## What to flag

For each finding, classify by severity (Critical / High / Medium / Low) and
confidence (High / Medium / Low).

### EF query inefficiency
- **N+1 queries:** loops that issue a query per iteration. Especially in
  content/block-list resolution paths and any `foreach` over an entity
  collection that lazy-loads.
- **Missing `AsNoTracking`:** read-only queries that return entities for
  display — should use `AsNoTracking` or `AsNoTrackingWithIdentityResolution`.
- **Materializing too early:** `.ToList()` / `.ToArray()` followed by `.Where`
  / `.Select` (filtering in memory instead of in SQL).
- **Missing `Include`:** queries that return an entity then access a navigation
  property, causing a second round-trip.
- **Over-`Include`:** loading a deep graph when only a few fields are needed
  — should be a projection (`.Select(x => new { ... })`).
- **`.Count()` vs `.Any()`:** existence checks using `.Count() > 0` instead of
  `.Any()`.
- **`.First()` / `.Single()` without `OrderBy`:** non-deterministic, and on
  some providers can pick a worse plan.
- **Pagination:** list endpoints without `Skip`/`Take`.
- **`.Where(x => x.Deleted == false)` not pushed to the query:** check that
  soft-delete filters are applied as `IQueryable` operations, not post-`ToList`
  filters.
- **Repeated identical queries** in a single request (no caching, no
  memoization).

### Cache misuse
- Cache keys that don't include enough state to be unique (collisions across
  unrelated queries → wrong data returned).
- Cache **prefix invalidation** that is too coarse (clears more than needed)
  or too narrow (misses entries that should be invalidated).
- Items written to cache that are never invalidated and depend on data that
  changes (stale data risk + memory growth).
- Items cached that shouldn't be — request-specific data, user-specific data
  in a shared cache, etc.
- Missing cache on hot read paths.

### Reflection / discovery overhead
- `ExtensionManager.GetInstances<T>()` or similar reflection-heavy calls in a
  request path without caching the result.
- `Assembly.GetTypes()` / `typeof(...).GetMethods()` in hot paths.
- LINQ over reflection metadata recomputed per call.

### Allocation hotspots in request paths
- `string.Concat` / `+` in tight loops (use `StringBuilder` or string
  interpolation with `DefaultInterpolatedStringHandler`).
- Frequent boxing (`object` collections of value types).
- `Enum.Parse` / `Enum.GetValues` in hot paths.
- Regex compiled per-call instead of `[GeneratedRegex]` / static field.
- LINQ chains creating intermediate collections in hot paths where a simple
  loop would do.

### I/O in hot paths
- File system access on every request that could be cached.
- HTTP calls without `IHttpClientFactory` (each `new HttpClient()` reserves
  a socket).
- Synchronous I/O on the request thread.

### Razor / Blazor rendering
- Components doing expensive work in `OnParametersSet` (runs on every parent
  re-render).
- Large `@foreach` loops without `@key`.
- `ShouldRender()` not overridden where it would help.
- Inline lambdas in render trees that capture state and force re-renders
  (`@onclick="() => Handler(item)"` patterns where appropriate).

### Middleware ordering / startup cost
- Anything that does meaningful work in `Configure` / `app.Use(...)` per
  request that could be cached.
- The middleware order in `ZauberSetup.cs` is documented as intentional —
  flag only if you can show a specific pipeline change that would help, not
  just "different is faster."

### Provider-specific issues
- `string.Compare` / `EF.Functions.Like` patterns that are fast on one
  provider and slow on another.
- Use of `DateTime.Now` in queries (not translatable cleanly across providers
  — should be `DateTime.UtcNow`, often).
- Case sensitivity differences (Postgres collation vs SQL Server).

## What NOT to flag

- The `GetAwaiter().GetResult()` in `ZauberSetup.cs` (one-time startup, see
  CLAUDE.md).
- The middleware ordering itself — only flag if you have a concrete,
  request-path-relevant suggestion.
- Style-level LINQ preferences (e.g. method syntax vs query syntax).
- Premature micro-optimizations that wouldn't show up in a flame graph.
- Anything that requires a benchmark you can't run — mark Low confidence
  and explain.

## Workflow

This is a **find-and-fix** audit. Two phases:

### Phase 1 — Audit
Walk the codebase, identify findings, and write them to
`.audit-reports/03-performance.md` (structure below). Order by severity then
confidence.

### Phase 2 — Fix
Apply fixes for findings that meet ALL of these criteria:
- Severity is **Critical** or **High** in a hot path (per-request /
  per-render), OR Medium with High confidence in a hot path.
- The fix is mechanical: add `AsNoTracking`, hoist a `Where` before
  materialization, replace `.Count() > 0` with `.Any()`, add an `Include`
  to fix N+1, project to a DTO instead of full entity, cache a reflection
  result in a static field, swap `new HttpClient()` for
  `IHttpClientFactory`, etc.
- The fix doesn't change semantics (no risk of returning different data).
- The fix doesn't require new infrastructure (don't add Redis, don't add
  output caching middleware — that's design).

**Skip fixing** (leave in the report, marked `Status: not fixed`) when:
- Severity is Low or finding is in a cold path (startup, admin once-per-page).
- Confidence is Low.
- The fix requires schema / migration changes.
- The fix needs a benchmark to validate (you can't run benchmarks here).
- The fix would change a public API or service contract.

After all fixes:
- Run `dotnet build ZauberCMS.sln -c Release` and confirm it succeeds.
- If the build fails, fix the build error, build again, repeat.
- Do **not** run migrations or the app.

## Output

Write a single file: `.audit-reports/03-performance.md`

```markdown
# Performance Audit

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
- **Hot path?** Yes (per-request / per-render) / No (cold path)
- **Issue:** One paragraph.
- **Why it matters:** Frequency + rough impact.
- **Fix applied:** What you changed. Omit if not fixed.
- **Why not fixed:** Reason. Omit if fixed.
- **Notes:** Anything you weren't sure about.

(repeat per finding)

## Hot paths reviewed
List the files/methods you specifically inspected as hot paths.

## Files modified
List every file you edited with a one-line summary.

## Areas you did not fully cover
List any directories / files you didn't get to and why.
```

If a category has no findings, write "No issues found."

## Hard rules

- Edits allowed only for findings that pass the Phase 2 criteria.
- Do not change query semantics. If you're not sure two queries return the
  same set, don't replace one with the other.
- No new infrastructure, no new dependencies.
- `dotnet build` only.
- If a fix breaks the build and you can't repair it in scope, **revert** and
  mark `Status: not fixed — build broke, reverted`.
- Cite file paths and line numbers for every finding.
