# Audit: Bugs & Correctness

## Your task

Read-only audit of the ZauberCMS codebase for bugs, correctness issues, and
edge cases. This is the catch-all "things that are wrong but aren't async,
memory, perf, or security." Produce a structured findings report. **Do not
edit any code.** Do not run the application.

## Repo context you need

- ZauberCMS is a .NET 10 Blazor CMS, modeled on Umbraco.
- **Soft-delete only** (`Deleted` flag). Every list query MUST filter
  `Deleted == false` for live data, or `Deleted == true` for the recycle bin.
  Missing or inverted filters return wrong data.
- Three EF providers (SQL Server / SQLite / PostgreSQL). Migrations live in
  three separate folders. Schema drift between providers is a known recurring
  problem.
- Cache invalidation is **by prefix** (`ICacheService.ClearCachedItemsWithPrefix`).
  Wrong prefix = stale data after writes.
- Property editor settings are **JSON strings in the DB**, round-tripped via
  `Settings.FromJson<TModel>()`. New editors that don't follow this pattern
  silently fail to round-trip.
- Front-end view resolution: `Content.ViewComponent` is matched against
  `IContentView` **class names**. Renaming a view class breaks rendering for
  any content already pointing at the old name.
- `CatchAll.razor` (`ZauberCMS.Routing/CatchAll.razor`) must remain in
  `ZauberCMS.Routing` and that assembly must load last (`AssemblyManager`
  enforces this). Flag any change that risks the load order.
- Identity tables prefixed `Zauber*` (e.g. `ZauberUsers`). Raw SQL in the repo
  (rare) must use the prefixed names.
- Login has rate limiting (10 attempts / 5 minutes).
- `AppState` events: `OnContentChanged`, `OnUserChanged`, `OnMediaChanged`,
  per-action `OnXSaved` / `OnXDeleted`. Missed publishes = consumers never
  refresh.

## What to flag

For each finding, classify by severity (Critical / High / Medium / Low) and
confidence (High / Medium / Low).

### Soft-delete bypasses
- Any query against `Content`, `Media`, `User`, `ContentType`, etc. that
  doesn't filter `Deleted` (in either direction).
- Queries where the soft-delete filter is applied **after** materialization
  (correctness is preserved but it's a smell — flag as Low).
- Update / delete paths that mutate soft-deleted records as if they were
  live.

### Null safety
- `Content.GetValue<T>("alias")` consumers that don't handle nulls /
  defaults for unknown aliases.
- Service methods returning `T?` where callers dereference without checking.
- Razor markup that does `@something.Property` where `something` could be
  null after a missed lookup.
- LINQ `.First()` / `.Single()` where `.FirstOrDefault()` / `.SingleOrDefault()`
  + null check is correct (or vice versa — `.FirstOrDefault()` with no null
  check downstream).

### Cache key / invalidation correctness
- Prefix passed to `ClearCachedItemsWithPrefix` doesn't match the prefix used
  when items were added.
- Cache write happens inside a code path that bypasses the corresponding
  invalidation (write to DB without clearing cache, or vice versa).
- Cache key built from a value that doesn't fully identify the result
  (causing cross-talk between users / cultures / content states).
- User-specific or culture-specific results cached under a non-keyed prefix.

### Multi-provider drift
- Entity configurations or migrations that exist in one provider folder
  (`Migrations/SqlServer/`, `Migrations/SqLite/`, `Migrations/PostgreSql/`)
  but not the others.
- Provider-specific column types / constraints that differ in schema between
  providers (e.g. `nvarchar(max)` vs `text`).
- Raw SQL strings that aren't valid on all three providers.
- Date / time handling where one provider stores UTC and another local.

### Settings round-trip
- `IContentProperty` implementations whose `SettingsComponent` doesn't
  parse/serialize JSON via `Settings.FromJson<TModel>()`.
- Settings models that aren't `[Serializable]`-equivalent for JSON (private
  setters, missing parameterless ctor, etc.).
- New editors missing from any of the registration / discovery checks.

### Auth / identity edge cases
- Endpoints under `/admin` or admin-only services that don't check
  authentication / authorization.
- Role checks using string literals where a constant exists.
- Lockout state not respected (login bypass scenarios).
- Password reset / email confirm flows with token reuse, missing expiry,
  or weak comparisons.
- OAuth callback handlers (`IExternalAuthenticationProvider` impls — Google /
  Facebook / Microsoft) that don't validate state or trust claims they
  shouldn't.
- Admin IP whitelist (`GlobalSettings.AllowedAdminIpAddress`) — enforcement
  in `SectionLayout.razor` is documented; flag any admin entry point that
  bypasses it.

### Localization / culture
- Strings hard-coded in admin UI that should go through dictionary entries.
- `DateTime.Now` / `DateTime.UtcNow` confusion (CMS audit timestamps should
  be UTC).
- Culture not flowing through `CultureMiddleware` for a code path that
  expects it (e.g. content lookups bypassing the middleware-set context).

### File / media handling
- Path traversal in media paths (`..` not stripped, absolute paths accepted).
- File type validation against extension only, not content type / magic bytes.
- Upload size enforcement gaps (`GlobalSettings.MaxUploadSize` not checked
  in some upload path).
- `IStorageProvider` consumers assuming the default `DiskStorageProvider`
  (e.g. casting, reading filesystem-specific properties).

### Slug / URL handling
- Slug normalization inconsistencies between write (`ContentService.Save`)
  and read (`GetContentFromRequestAsync`).
- Trailing slash, leading slash, case sensitivity divergence.
- Reserved paths (`/admin`, `/_content/`, `/api/`) not blocked from
  user-supplied slugs.

### `AppState` event publishing
- Save / delete / update paths in services that don't publish the
  corresponding `OnX...` event (consumers won't refresh).
- Events published with wrong payload (e.g. publishing the pre-mutation
  entity instead of the post-mutation one).

### Migrations / DB
- Migration files that exist in one provider folder without the equivalent
  in the other two.
- Migrations that drop columns containing data without warning / preservation.
- Down-migrations that don't actually reverse the up.

### General correctness smells
- Off-by-one in pagination (Skip(page * size) vs Skip((page - 1) * size)
  inconsistencies).
- Boundary conditions: empty collections, single-element edge cases.
- Comparing `string` with `==` where culture / case sensitivity matters
  (use `StringComparison.Ordinal` / `OrdinalIgnoreCase`).
- `Equals` / `GetHashCode` mismatches on value-equal entities.
- `try` / `catch` swallowing exceptions silently (no log, no rethrow,
  no handling).

## What NOT to flag

- Code style / naming preferences.
- Things already documented as intentional in CLAUDE.md (the sync startup
  query, middleware ordering, both `ProjectReference` + `PackageReference`
  on Razor csprojs, etc.).
- The committed `ZauberCMS.Web/app.db` (it's a dev artifact).
- Stale `ZauberCMS.StarterSite/` directory (CLAUDE.md says ignore).

## Workflow

This is a **find-and-fix** audit. Two phases:

### Phase 1 — Audit
Walk the codebase, identify findings, and write them to
`.audit-reports/04-correctness.md` (structure below). Order by severity then
confidence.

### Phase 2 — Fix
Apply fixes for findings that meet ALL of these criteria:
- Severity is **Critical** or **High**, OR Medium with High confidence AND
  a clear, verifiable trigger.
- The fix is local: add a missing `Deleted == false` filter, add a null
  check, fix a wrong cache prefix, publish a missing `AppState` event after
  save, fix a `FromJson<T>` round-trip, fix off-by-one in pagination, fix
  string comparison flags.
- The fix doesn't change behaviour beyond closing the bug.

**Skip fixing** (leave in the report, marked `Status: not fixed`) when:
- Severity is Low.
- Confidence is Low or no clear trigger.
- The fix needs schema / migration changes (those are sensitive — three
  providers, three migrations, never hand-roll one).
- The fix would touch admin UX or change user-visible workflows (those need
  human review).
- The bug is in raw SQL that differs across providers — flag, don't fix.

**Special note on migrations:** if a finding is "migration drift between
SqlServer / SqLite / PostgreSql folders," **do not fix it.** Per CLAUDE.md,
migrations must be regenerated via `migrations.ps1` to stay in sync — flag
the drift in the report, leave the fix for a human.

After all fixes:
- Run `dotnet build ZauberCMS.sln -c Release` and confirm it succeeds.
- If the build fails, fix the build error, build again, repeat.
- Do **not** run migrations or the app.

## Output

Write a single file: `.audit-reports/04-correctness.md`

```markdown
# Correctness & Bugs Audit

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
- **Trigger:** What input / state surfaces the bug.
- **Why it matters:** User-visible impact.
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
- **Never edit migration files.** Flag drift, do not fix it.
- No schema changes (no new tables, no new columns) — per CLAUDE.md
  this is forbidden without a deliberate decision.
- `dotnet build` only.
- If a fix breaks the build and you can't repair it in scope, **revert** and
  mark `Status: not fixed — build broke, reverted`.
- Every finding must cite a file path and line number, and Critical/High
  findings must include a concrete reproduction trigger.
