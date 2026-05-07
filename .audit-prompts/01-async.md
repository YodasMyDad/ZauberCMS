# Audit: Async / Threading Correctness

## Your task

Read-only audit of the ZauberCMS codebase for async, threading, and concurrency
correctness issues. Produce a structured findings report. **Do not edit any
code.** Do not run the application. Do not run migrations.

## Repo context you need

- .NET 10 Blazor app. Front-end is **static SSR** (no render mode); admin
  (`/admin`) is **InteractiveServer**.
- Three EF Core providers (SQL Server / SQLite / PostgreSQL) with three
  `DbContext` types — see `ZauberCMS.Core/Data/`.
- Services use the `IServiceScopeFactory` pattern (singletons create scopes
  per call). Pattern in `ZauberCMS.Core/Content/Services/ContentService.cs` etc.
- `AppState` (`ZauberCMS.Core/Shared/AppState.cs`) is a **singleton** that uses
  `WeakEventManager<T>` for cross-component pub/sub.
- Cache abstraction: `ICacheService`
  (`ZauberCMS.Core/Shared/Services/DefaultCacheService.cs`) — Redis when
  configured, otherwise `IMemoryCache`.
- Bootstrap is `ZauberCMS.Core/ZauberSetup.cs`. **Note:** it intentionally runs
  one async query synchronously via `GetAwaiter().GetResult()` to populate
  cultures before `RequestLocalization` is registered. This is documented and
  intentional — flag it only if you find the call site has actually drifted, not
  just because it's sync-over-async.

## What to flag

For each finding, classify by severity (Critical / High / Medium / Low) and
confidence (High / Medium / Low).

### Sync-over-async
- `.Result`, `.Wait()`, `.GetAwaiter().GetResult()` calls anywhere in
  request-path code, services, middleware, or Razor components. Exception:
  the documented one in `ZauberSetup.cs` (don't flag unless it has changed).
- Synchronous I/O in async methods (`File.ReadAllText` instead of
  `File.ReadAllTextAsync`, `Stream.Read` vs `ReadAsync`, etc.).

### `async void`
- Any `async void` that is not an event handler signature. List each one with
  the calling site if findable.

### Fire-and-forget Tasks
- Discarded `Task` returns: `_ = SomeAsyncMethod()`, `SomeAsyncMethod();` with
  the warning suppressed, or `Task.Run(() => ...)` with no continuation.
- Unhandled exceptions in fire-and-forget tasks (no `.ContinueWith` for fault,
  no `try/catch` inside the lambda).

### DbContext concurrency
- Same `DbContext` instance shared across `await` boundaries that could resume
  on different threads in a way that violates `DbContext`'s
  not-thread-safe contract. Particularly: parallel `Task.WhenAll` over a single
  context, or storing a context on a singleton.
- Scoped `DbContext` captured by a singleton without going through
  `IServiceScopeFactory.CreateScope()`.

### `IServiceScopeFactory` pattern correctness
- Each `using var scope = _serviceScopeFactory.CreateScope()` should resolve
  scoped services from `scope.ServiceProvider`, not from a captured root
  provider. Flag anywhere this is violated.
- Scopes must be disposed (look for `CreateScope()` without `using`).

### Locking and async
- `lock(...)` blocks containing `await` (illegal — won't compile, but check
  for `Monitor.Enter` patterns that do the same thing manually).
- `SemaphoreSlim.Wait()` (sync) used in async methods — should be `WaitAsync`.
- `lock` around mutable shared state where the lock is dropped before async
  continuation completes (TOCTOU on the protected state).

### `CancellationToken` propagation
- Public async methods that take no `CancellationToken` and call other
  cancellable methods without forwarding one. Most service methods in
  `ZauberCMS.Core/*/Services/*` are candidates — check whether they accept and
  forward tokens to EF queries (`ToListAsync(ct)`, etc.).

### Thread-unsafe shared state
- Static `Dictionary<,>` / `List<>` mutated from request-path code without
  synchronization (should be `ConcurrentDictionary` or behind a lock).
- Singletons mutating non-thread-safe collections.

### Blazor-specific
- `StateHasChanged()` called from a non-UI thread without `InvokeAsync(...)`.
- `OnInitializedAsync` doing blocking calls.
- Event subscriptions inside Blazor components without `IDisposable` /
  `IAsyncDisposable` unsubscribing them — this overlaps with the memory audit;
  list under threading only if there's a *concurrency* angle (e.g. callback
  fires post-disposal).

## What NOT to flag

- The intentional sync `GetAwaiter().GetResult()` in `ZauberSetup.cs` for
  culture init (see context above).
- `WeakEventManager<T>` itself — it's the chosen pub/sub primitive.
- `Task.Run` in CLI / startup code paths (only flag in request paths).
- Missing `ConfigureAwait(false)` in ASP.NET Core / Blazor code — irrelevant
  since .NET 5+.

## Workflow

This is a **find-and-fix** audit. Two phases:

### Phase 1 — Audit
Walk the codebase, identify findings, and write them to
`.audit-reports/01-async.md` (structure below). Order by severity then
confidence.

### Phase 2 — Fix
Apply fixes for findings that meet ALL of these criteria:
- Severity is **Critical** or **High**, OR Medium with High confidence.
- The fix is mechanical / non-architectural (e.g. add `await`, swap
  `.Result` for `await`, forward `CancellationToken`, fix `IServiceScopeFactory`
  usage, dispose a scope, switch to `WaitAsync`).
- The fix doesn't require a new abstraction, new service, or behaviour
  change beyond what the issue is.
- You're confident the fix doesn't break callers — if a public signature
  needs to change, update all call sites in the same pass.

**Skip fixing** (leave in the report, marked `Status: not fixed`) when:
- Severity is Low.
- Confidence is Low.
- The fix needs design judgement (e.g. should this be `Task.Run` vs
  `Channel<T>` vs a hosted service?).
- The fix would touch >10 files or cross a public API boundary.

After all fixes are applied:
- Run `dotnet build ZauberCMS.sln -c Release` and confirm it succeeds.
- If the build fails, fix the build error, build again, repeat.
- Do **not** run migrations, do not run the app, do not run tests
  (there aren't any meaningful ones to gate on).

## Output

Write a single file: `.audit-reports/01-async.md`

```markdown
# Async / Threading Audit

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
- **Why it matters:** Concrete impact, not theoretical.
- **Fix applied:** What you changed (file:line and one-line summary).
  Omit if not fixed.
- **Why not fixed:** If skipped, the reason. Omit if fixed.
- **Notes:** Anything you weren't sure about.

(repeat per finding)

## Files modified
List every file you edited, one per line, with a one-line summary of the
change.

## Areas you did not fully cover
List any directories / files you didn't get to and why.
```

If a category has no findings, include the heading and write "No issues found."

## Hard rules

- Edits are allowed, but **only** for findings that pass the Phase 2
  criteria above. Anything you fix must appear in the report with
  `Status: FIXED` and a `Fix applied` line.
- Do not introduce new abstractions, new services, or new dependencies.
- Do not refactor surrounding code that isn't part of the finding.
- Do not run the app or migrations. `dotnet build` only.
- If the build breaks and you can't fix it within the scope of this audit,
  **revert your changes for that finding** and mark it `Status: not fixed —
  build broke, reverted`.
- Cite specific file paths and line numbers for every finding.
