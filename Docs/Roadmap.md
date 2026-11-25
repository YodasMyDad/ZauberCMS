# Rework 2026 — Roadmap & Handoff

**Start here when resuming the rework.** Read order: [AGENTS.md](../AGENTS.md) → this file →
[.audit/report.md](../.audit/report.md) (findings by ID) → the Docs file for the area you're touching.

## Status (2026-11-25)

- Branch **`rework-2026`** (pushed). All rework happens here. **Do not merge or push to `main-static`** until the
  owner says so — every push there publishes to nuget.org.
- Done: docs consolidated and fact-checked; first full audit run. **131 open findings** (1 Critical, 29 High,
  64 Medium, 37 Low), IDs `ZA-001`…`ZA-131`.
- **No code has been changed yet.** Nothing in the audit report is fixed.
- Findings come from reading code + a real build/package scan. Nothing was run in a browser or against
  PostgreSQL/Redis — items marked "needs repro" must be reproduced before or while fixing.
- No test project exists.

## Owner decisions (2026-11-25)

| # | Decision | Affects |
| --- | --- | --- |
| D1 | **PostgreSQL and Redis caching are supported and must work.** Treat their defects as real bugs, not edge cases. | ZA-050, ZA-007, ZA-051, ZA-014, ZA-055 |
| D2 | **Replace "first registered user becomes admin" with an explicit setup step — a polished installer.** | ZA-004 (also ZA-023, ZA-024) |
| D3 | **Media gets a recycle bin like content:** delete → recycle bin (restorable); deleting *from* the bin is permanent (row **and** file removed). A direct permanent-delete option is also allowed. | ZA-006, ZA-053 |
| D4 | **Dependencies: first bring everything (Radzen included) to the latest versions that run on .NET 10, then test the app.** Only once it all works well: move to .NET 11 and test again. | ZA-102, ZA-130 |
| D5 | Block List editor is to be **simplified/redesigned**, not patched indefinitely. | ZA-034…ZA-043, [BlockListEditor.md](BlockListEditor.md) §6 |

## Working rules for the rework

1. One phase (or one finding group) per change; keep diffs reviewable. Commit to `rework-2026` only when asked.
2. Every fix: follow [Standards.md](Standards.md), grep all callers of anything whose signature/default changes,
   `dotnet build ZauberCMS.sln -c Release` clean, and state how it was verified (run the app for UI behaviour).
3. After fixing, update `.audit/report.md` (move the finding to "Fixed", keep the ID) and any doc the change makes
   stale. Re-run `/audit changed` at the end of each phase.
4. Schema changes: three migrations via `migrations.ps1` — and from Phase 6 on, verify on PostgreSQL too.
5. Public API: obsolete, don't break, until the v5 phase (Phase 11) where breaks are batched deliberately.
6. Add a regression test with each fix once the test project exists (Phase 2).

## Phases (in order)

### Phase 0 — Critical hotfix
- **ZA-044**: add `[Authorize(Roles = Admin)]` to the three `/admin/seo/*` pages (or `Seo/_Imports.razor`).
  ⚠ This hole is in the **published 4.1.3 packages**. Ask the owner whether to ship it as a hotfix on
  `main-static` now rather than waiting for the rework.

### Phase 1 — Dependencies to latest on .NET 10 (D4)
- Patch/minor first: Microsoft.* 10.0.7 → latest 10.0.x, Npgsql EF → latest 10.0.x, HtmlSanitizer (vulnerable),
  HtmlAgilityPack, FluentValidation, DynamicLinq, BlazorMonaco, TinyMCE.Blazor. Re-run
  `dotnet list package --vulnerable --include-transitive` until clean (ZA-102); pin transitive packages if needed.
- Then majors that still target .NET 10, each as its own change: **Radzen.Blazor** (10.3.2 → latest; read its
  breaking-change notes — Core's public interfaces expose Radzen types, ZA-091), MailKit 5, HtmlSanitizer 11,
  ZauberCMS.RTE 3, Serilog.AspNetCore. Do **not** take 11.0.x Microsoft/EF packages yet.
- **Blazored.Modal**: `dotnet list package --outdated` reports "Not found at the sources". Investigate (unlisted?
  abandoned?) and propose: keep pinned, fork, or replace with Radzen dialogs/side panels. Owner decides.
- Good moment to add `Directory.Packages.props` (central versions) + `global.json` — optional, own commit.
- Exit: build clean with 0 vulnerability warnings; app runs; manual smoke test of admin (content, media, users,
  structure, settings, SEO) and front-end on SQLite.

### Phase 2 — Test foundation
- Create `tests/ZauberCMS.Core.Tests` (xUnit, EF Core SQLite) and wire into CI build job (PR validation).
- First tests = the list under "Coverage" in the audit report. They pin the bugs fixed in Phases 3–5.

### Phase 3 — Broken admin actions (small, high impact)
ZA-001 (recycle-bin restore / final delete / open), ZA-045 + ZA-088 (delete user), ZA-076 (Culture & Domains false
error), ZA-103 (email confirmation), ZA-058 (layout re-render wipes edits), ZA-081 (silent context-menu failures).

### Phase 4 — Access control & identity
ZA-053 (restricted media public when recycled / case bypass), ZA-052 (`refreshsignin`), ZA-002 (external sign-up —
needs a live provider test), ZA-059 (lockout), ZA-019 + ZA-020 (SSRF / dynamic LINQ), ZA-003 (host-header links),
ZA-060 (proxy headers, IP allow-list in middleware), ZA-021/ZA-022/ZA-005 (sanitiser, uploads, SVG — some need
owner decisions; ask).

### Phase 5 — Content integrity
ZA-046 (duplicate URL breaks front-end), ZA-047 (drafts discarded), ZA-048 (recycle parent/children),
ZA-049 (copy shares draft), ZA-065, ZA-066, ZA-067 (publish/slug/nested-URL leaks), ZA-070, ZA-074, ZA-075, ZA-079,
ZA-016.

### Phase 6 — PostgreSQL & Redis made real (D1)
- Stand up PostgreSQL and Redis locally (containers) and run the app on each; add them to CI if feasible.
- PostgreSQL: ZA-050 (invalid index filter in initial migration + missing indexes), ZA-007 (unquoted raw SQL),
  ZA-017 (`ToLower` search), ZA-122 (design-time factory file case).
- Redis: ZA-051 (entity graphs through JSON → restricted pages exposed, hashes in cache), ZA-014 + ZA-055 (key
  registry: unbounded, lost on restart, race), ZA-072 (cache key ignores includes). Likely one redesign of
  `DefaultCacheService` (DTO caching or memory-for-graphs + Redis-for-invalidation; tag/prefix invalidation that is
  multi-node safe).

### Phase 7 — Installer (D2)
- Replace `RoleExtensions` first-user promotion with a setup flow. Requirements: runs only while the site is
  uninitialised; cannot be re-triggered by soft-deleting users; creates the first admin explicitly; static SSR page
  with rate limiting + antiforgery; clear "already installed" state.
- "Slick" scope to **agree with the owner before building**: e.g. DB provider + connection test, admin account,
  site name/language, optional starter content, final summary. Also settle `AdminEmailAddresses` auto-promotion
  (ZA-023) and a switch for public registration (ZA-024).

### Phase 8 — Media recycle bin (D3)
- Media section recycle bin: list, restore, permanent delete (removes file via `IStorageProvider`), empty bin.
- Recycled media must not be publicly served (ties to ZA-053); notifications fixed (ZA-013).

### Phase 9 — Block List redesign (D5)
Follow [BlockListEditor.md](BlockListEditor.md) §6: edit session → `SaveContentTreeAsync` → static previews →
`INestedContentReferenceProvider`. Write `BlockListState`/tree-save tests first. Closes ZA-008…ZA-010,
ZA-034…ZA-043.

### Phase 10 — Performance & async
ZA-057, ZA-061…ZA-064, ZA-056, ZA-068, ZA-069, ZA-071, ZA-073 (queries/caching); ZA-054 + ZA-082 with the
`PaginatedList` async refactor (must land together); ZA-083, ZA-084, ZA-086, ZA-080; then the
`IServiceScopeFactory` → `IDbContextFactory` / single-context refactor (ZA-011).

### Phase 11 — Engineering baseline, API clean-up, v5
`Directory.Build.props`, analyzers, `.editorconfig`, `.gitattributes`, CI hardening (ZA-089, ZA-109), packaging
(ZA-087, ZA-094, ZA-118, ZA-119), error handling (ZA-097, ZA-098), then the deliberate breaking batch for v5:
internalise non-contract types (ZA-092), move Radzen-typed contracts out of Core (ZA-091), remove the unwired MVC
path (ZA-090), `ICurrentUserAccessor` (ZA-093), naming conventions (ZA-124). Remaining Mediums/Lows by area.

### Phase 12 — .NET 11 (D4)
Retarget to `net11.0`, move Microsoft/EF/Npgsql packages to 11.x, full retest on SQLite, SQL Server, PostgreSQL and
Redis, then release planning with the owner.

## Open questions for the owner

- Hotfix ZA-044 on `main-static` now? (Phase 0)
- Blazored.Modal: keep, fork or replace? (Phase 1, after investigation)
- Installer scope (Phase 7).
- SVG uploads, sanitiser policy for `iframe`/`data:`, CAPTCHA/registration switch (Phase 4).
- Is a v5 major acceptable as the vehicle for the breaking changes in Phase 11?
