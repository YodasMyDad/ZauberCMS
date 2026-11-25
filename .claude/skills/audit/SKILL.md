---
name: audit
description: "Full ZauberCMS codebase audit (async, lifetimes, data/perf, correctness invariants, security, Blazor/render-mode, architecture/API, packaging, dependencies, docs drift). Use when the user says /audit, 'run the audit', 'audit the codebase', or before a release. Args: scope = full | changed (default) | <category letter(s)>; mode = report (default) | fix."
---

# ZauberCMS Audit Runbook

One runbook, one report: **`.audit/report.md`** (overwritten each run; history is in git). Any agent/tool can follow
this file. Read `AGENTS.md` and `Docs/Standards.md` first — they define the bar being audited against.

**Arguments**: `scope` — `changed` (default: files changed since the report's *Audited commit*), `full`, or category
letters (e.g. `C,D`). `mode` — `report` (default, no code edits) or `fix` (apply fixes meeting §5 criteria).

## 0. Prepare

1. `git pull`; `git status --short` must show no unrelated edits you'd mix in (ignore `ZauberCMS.Web/app.db`).
2. Read `.audit/report.md`. Note *Audited commit* and the open findings (stable IDs `ZA-NNN`).
3. Resolve scope: `changed` → `git diff --name-only <audited-commit>..HEAD`; if the report has no commit or > ~150
   files changed, use `full`.

## 1. Reconcile open findings (always, before new work)

For every open finding: re-read the cited code. Mark **resolved** (fixed elsewhere — cite commit), **still open**
(update `path:line`), or **regressed/changed**. Also scan `git log <audited-commit>..HEAD` for changes that flip a
default or a parameter used by an open finding — regressions hide there.

## 2. Deterministic checks (cheap, run directly; keep only summaries)

| Check | Command / method |
| --- | --- |
| Build + warnings | `dotnet build ZauberCMS.sln -c Release` → pass/fail, warning count by code (compare to last report) |
| Vulnerable packages | `dotnet list ZauberCMS.sln package --vulnerable --include-transitive` |
| Outdated packages | `dotnet list ZauberCMS.sln package --outdated` (report majors separately) |
| Version alignment | Same versions of shared packages across the 4 csprojs; PackageReference chain matches `<Version>` |
| Migration parity | Same migration names in `Data/Migrations/{SqlServer,SqLite,PostgreSql}`; snapshots all updated in the same commit |
| Config parity | Keys in `ZauberCMS/appsettings.json`, `ZauberCMS.Web/appsettings.json`, template site `appsettings.json` agree; no real secrets |
| Docs drift | Every backticked path/type/member in `AGENTS.md` and `Docs/*.md` exists (grep); flag stale claims |
| Repo hygiene | No tracked local files (`*.user`, `settings.local.json`, stray `*.db` outside `ZauberCMS.Web`) |

## 3. Category review (fan out)

Spawn **one subagent per category in scope** (max 6, in parallel). Give each: the category checklist below, the
scope file list, the "Not findings" list, and the finding schema. Each returns findings only — no file dumps.

Finding schema: `severity (Critical/High/Medium/Low) | confidence (High/Medium/Low) | title | path:line | issue (1-3
sentences) | trigger or attack scenario (required for Critical/High) | suggested fix | fix risk (public API? schema?
UX? callers affected?)`.

### A. Async, threading, lifetimes, memory
- Sync-over-async (`.Result`, `.Wait()`, `GetAwaiter().GetResult()`, `Task.Run(...).GetAwaiter()`), sync EF/I/O in async methods.
- `async void` outside event handlers; handlers without try/catch+log. Fire-and-forget (`_ = X()`, unobserved `Task.Run`).
- `CancellationToken` missing on public async APIs or not forwarded to EF/HTTP.
- DbContext shared across `Task.WhenAll`, held by a singleton, or outliving its scope; `CreateScope()` without `using`.
- Scoped service captured by a singleton; static mutable collections mutated per request; unbounded static caches.
- Subscriptions (`AppState`, `TreeState`, `LocationChanged`, `EditContext`) without matching unsubscribe in `Dispose`;
  subscribing in `OnParametersSet`. `StateHasChanged` from other threads without `InvokeAsync`.
- Undisposed `CancellationTokenSource`, timers, `IJSObjectReference`, `DotNetObjectReference`, streams.
- `SemaphoreSlim.Wait()` in async code; locks across `await`.

### B. Data access & performance
- Hot path (`CatchAll` → `EntryPage` → views, `CultureMiddleware`, `RestrictedMediaMiddleware`, image resize):
  extra DB round-trips, uncached queries, `GetInstances<T>()` per item.
- N+1 (esp. block lists, trees, pickers); `ToList()` before `Where`; missing `AsNoTracking` on reads (or applied after
  filters); over-`Include` where projection fits; `Count() > 0`; `First` without `OrderBy`; unbounded lists.
- Paging: 1-based `PageIndex` consistency, `Skip/Take` with stable order.
- Provider portability: `ToLower()` comparisons, `DateTime.Now`, raw SQL identifier quoting, `HasColumnType`.
- Cache keys missing user/culture/state dimensions; cache writes without matching invalidation.
- Regex not `[GeneratedRegex]`; `new HttpClient()`; allocations in loops; Blazor lists without `@key`; large lists
  without `Virtualize`.

### C. Correctness — ZauberCMS invariants
- Soft delete: every Content/Media/User query filters `Deleted` in the right direction; recycle-bin paths pass
  `IncludeDeleted`/`IsDeleted = true`; callers of any `Get*`/`Query*` whose filter default changed.
- Writes via `SaveChangesAndLog`; related cache prefixes cleared; `MapTo` covers every mutable property.
- `AppState` events raised exactly once per save/delete with the post-mutation entity.
- Editor settings round-trip (`GetPropertySettings`/`SavePropertySettings`/`FromJson<T>`); value formats stable.
- Persisted `Type.FullName`s (views, editors, settings fallbacks, hard-coded strings like the sitemap's SeoProperty).
- `CatchAll` in `ZauberCMS.Routing` and Routing scanned last.
- Block list: save order, hard-delete cascade, pending-changes propagation, versions/snapshots consistency.
- Null safety on `GetValue<T>`/`T?` returns; `FirstOrDefault` unchecked; duplicate-key `ToDictionary`.
- Slugs/URLs: write vs read normalisation, reserved paths (`/admin`, `/_content`, `/api`, `/Account`) blocked.
- Culture/language flow via `CultureMiddleware`; UTC timestamps; invariant culture in stored values.
- Swallowed exceptions; string comparisons without `StringComparison`; off-by-one.

### D. Security
- AuthZ on every admin page/component outside `Components/Admin/`, controllers, endpoints; IDOR on content/media/user/role.
- Admin IP whitelist bypass; rate limiting on every login-like/registration/reset page; lockout on all sign-in paths.
- Identity: first-user-admin, `AdminEmailAddresses` promotion, external login (`state`, verified email mapping,
  auto-link by email), security-stamp rotation on privilege change, cookie flags.
- Injection: `FromSqlRaw`/`ExecuteSqlRaw` with concatenation; Dynamic LINQ from browser strings without allow-list;
  `Type.GetType(input)`.
- XSS: `MarkupString`/`Html.Raw` without `IHtmlSanitizerService`; sanitiser policy (`iframe`, `data:`); JS `innerHTML`.
- CSRF: state-changing GETs; missing antiforgery on SSR forms.
- Uploads: extension + content sniffing, `MaxUploadSize` on every path, path traversal, SVG/HTML same-origin,
  `RestrictedMediaMiddleware` bypass, image-resize dimension DoS.
- SSRF (SEO checker, any URL fetch), open redirects (`returnUrl`), Host-header-built email links, `AllowedHosts`.
- Secrets in config/logs/UI; PII in logs; weak crypto (MD5/SHA1 for security, `Random` for tokens).
- Production hardening: detailed errors, HSTS/HTTPS, forwarded headers, data-protection key persistence (multi-node).
- CI: workflow permissions, pinned action versions, secret scope.

### E. Blazor, render modes, admin UX
- No Radzen / admin JS / `@rendermode` leakage into front-end views; interactive opt-ins are per component.
- SSR forms: `FormName`, `[SupplyParameterFromForm]`, antiforgery; prerender double-execution side effects.
- JS interop outside `OnAfterRenderAsync`; `JSDisconnectedException` handling on dispose.
- Every `IContentView` has `@layout`; components don't call EF directly.
- Admin a11y: labels on inputs, keyboard access to trees/context menus, focus in side panels, contrast.
- Raw exception text shown to users; notifications use the extension helpers.

### F. Architecture, public API, packaging, docs
- Breaking changes to public types/discovery interfaces/parameter classes without `[Obsolete]` path.
- New code following "Known deviations" in `Docs/Standards.md` instead of the target.
- Discovery: new extension types public/non-abstract; bootstrap plugins have parameterless ctors.
- Packaging: nupkg metadata parity, template scaffold builds against current packages, packed `build/settings.json`.
- Tests: list untested critical paths (soft delete, recycle bin, block list save, migrations, auth); note when a
  test project should be added (see Standards §9).
- Docs drift beyond §2: behaviour described in `Docs/*.md` that the code no longer does.

### Not findings (exempt)
Intentional sync culture query in `ZauberSetup`; middleware order; dual Project/PackageReference; `WeakEventManager`
design; missing `ConfigureAwait(false)`; tracked `ZauberCMS.Web/app.db`; code style preferences; anything already in
`Docs/Standards.md` → Known deviations (report once as a roll-up, not per occurrence).

## 4. Verify

Re-read the cited code for every Critical/High finding and any finding you intend to fix (or have a fresh subagent
verify it adversarially). Drop or downgrade anything not reproduced. Merge duplicates across categories. Match new
findings against open ones (same root cause → same ID).

## 5. Fix (mode = fix only)

Fix only when **all** hold: confidence High; severity Critical/High, or Medium with a concrete trigger; the change is
local (≈ ≤ 5 files); no public API, persisted format, schema/migration, auth/identity/cookie/antiforgery config, new
dependency, or user-visible workflow change; security fixes have a concrete attack scenario.

For each fix: change the minimum → **grep every caller of any member, parameter or default you changed** → build.
Build fails and can't be repaired in scope → revert, mark `not fixed — build broke`. Never edit migrations; never run
the app or migrations. Everything else stays open with a "why not fixed" line.

## 6. Report

Write `.audit/report.md` exactly in this shape (concise — one finding = a few lines):

```markdown
# ZauberCMS Audit Report
Audited commit: <sha> · Date: <UTC> · Scope: <full|changed|cats> · Mode: <report|fix>
Build: PASS/FAIL · Warnings: N (prev M) · Vulnerable packages: N · Outdated majors: N

## Summary
| Severity | Open | Fixed this run | Resolved since last run |

## Open findings
### ZA-NNN [Severity/Confidence] Title
- Where: `path:line` · Category: A–F · Since: <date>
- Issue / Trigger: …
- Fix: … · Blocked by: <public API | schema | product decision | …>

## Fixed this run / Resolved since last run
- ZA-NNN Title — commit/fix summary

## Deterministic checks
(table from §2, one line each)

## Coverage
Areas reviewed, areas skipped and why.
```

IDs are never reused. Finish by telling the user: counts by severity, top 5 open items, and anything that needs a
decision from them.
