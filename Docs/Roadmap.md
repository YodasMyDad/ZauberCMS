# Rework 2026 — Roadmap & Handoff

**Start here when resuming the rework.** Read order: [AGENTS.md](../AGENTS.md) → this file →
[.audit/report.md](../.audit/report.md) (findings by ID) → the Docs file for the area you're touching.

## Status (2026-11-25)

- Branch **`rework-2026`** (pushed). All rework happens here. **Do not merge or push to `main-static`** until the
  owner says so — every push there publishes to nuget.org.
- Done: docs consolidated and fact-checked; first full audit run (IDs now `ZA-001`…`ZA-136`).
- **Phase 0 done** (ZA-044), **Phase 1 done** (ZA-102, ZA-130, ZA-133: all packages on their latest .NET 10
  versions) and **Phase 2 done** (test projects + CI test step).
  **132 open findings** (0 Critical, 28 High, 66 Medium, 38 Low). **Next: Phase 3.**
- Findings come from reading code + a real build/package scan. Nothing was run in a browser or against
  PostgreSQL/Redis — items marked "needs repro" must be reproduced before or while fixing.
- Tests: `dotnet test --solution ZauberCMS.sln -c Release` (see [Standards.md](Standards.md) §9).

## Owner decisions (2026-11-25)

| # | Decision | Affects |
| --- | --- | --- |
| D1 | **PostgreSQL and Redis caching are supported and must work.** Treat their defects as real bugs, not edge cases. | ZA-050, ZA-007, ZA-051, ZA-014, ZA-055 |
| D2 | **Replace "first registered user becomes admin" with an explicit setup step — a polished installer.** | ZA-004 (also ZA-023, ZA-024) |
| D3 | **Media gets a recycle bin like content:** delete → recycle bin (restorable); deleting *from* the bin is permanent (row **and** file removed). A direct permanent-delete option is also allowed. | ZA-006, ZA-053 |
| D4 | **Dependencies: first bring everything (Radzen included) to the latest versions that run on .NET 10, then test the app.** Only once it all works well: move to .NET 11 and test again. | ZA-102, ZA-130 |
| D5 | Block List editor is to be **simplified/redesigned**, not patched indefinitely. | ZA-034…ZA-043, [BlockListEditor.md](BlockListEditor.md) §6 |

## Working rules for the rework

1. One phase (or one finding group) per change; keep diffs reviewable. **Commit to `rework-2026` at the end of each
   phase** (owner instruction 2026-11-25), once it builds and is verified; push only when asked. Never commit
   `ZauberCMS.Web/app.db`.
2. Every fix: follow [Standards.md](Standards.md), grep all callers of anything whose signature/default changes,
   `dotnet build ZauberCMS.sln -c Release` clean, and state how it was verified (run the app for UI behaviour).
3. After fixing, update `.audit/report.md` (move the finding to "Fixed", keep the ID) and any doc the change makes
   stale. Re-run `/audit changed` at the end of each phase.
4. Schema changes: three migrations via `migrations.ps1` — and from Phase 6 on, verify on PostgreSQL too.
5. Public API: obsolete, don't break, until the v5 phase (Phase 11) where breaks are batched deliberately.
6. Add a regression test with each fix. Where a skipped test is already waiting for the finding
   (`Skip = "ZA-NNN (open)…"`), remove the skip in the same change.

## Phases (in order)

### Phase 0 — Critical hotfix — DONE 2026-11-25
- **ZA-044** fixed: `[Authorize(Roles = Admin)]` added to the three `/admin/seo/*` pages. Verified by running the
  app (anonymous requests redirect to login); details in the audit report.
  ⚠ The hole is still in the **published 4.1.3 packages**: the fix is only on `rework-2026`. Shipping it as a
  hotfix on `main-static` is the owner's call (open question below).

### Phase 1 — Dependencies to latest on .NET 10 (D4) — DONE 2026-11-25
- Everything is on its latest version that runs on .NET 10 (Radzen 13, MailKit 5, HtmlSanitizer 11, ZauberCMS.RTE
  3.0.0, Microsoft.* 10.0.15, …). Build has 0 warnings; the vulnerability scan is clean. What was and was not
  tested is listed under ZA-130 in the audit report's "Fixed" section — read it before relying on an untested path.
- Held back on purpose: **Serilog.AspNetCore** stays on 10.0.0 (11.0.0 depends on Microsoft.Extensions 11.x) → Phase 12.
- **ZauberCMS.RTE**: migrated to the 3.x API on 3.0.0. A 3.x update is due shortly — the owner will say when to
  take it; then remove the 3.0.0 workarounds (ZA-136). 3.x strips iframes from stored content (ZA-134); **embeds
  arrive with RTE 4 at the .NET 11 move (Phase 12)** — owner decision.
- **Blazored.Modal** (deprecated, repo archived) is now **vendored**: its source lives in `ZauberCMS.Core/Modal/`
  (same namespaces), the NuGet package is gone, and it is ours to maintain (ZA-133). Radzen was ruled out because its
  side dialogs cannot nest. Release notes must tell sites to drop any direct `Blazored.Modal` package reference.
- Not done (optional, own commit): `Directory.Packages.props` + `global.json` — now part of Phase 11.
- Still to smoke-test by hand when convenient: media insert in the editor (sample DB has no media), two editors on
  one page, the user editor, large content, sending an email.

### Phase 2 — Test foundation — DONE 2026-11-25
- `tests/ZauberCMS.Core.Tests` and `tests/ZauberCMS.Components.Tests` (xUnit v3) are in the solution; CI runs them
  in the build job, which the publish job depends on. 54 tests: 50 pass, 4 are skipped because they describe open
  bugs (ZA-011, ZA-036, ZA-045, ZA-118) — each was seen failing first; remove the skip with the fix.
- Covered: DI container builds and validates for all three providers; an empty SQLite database migrates; no model
  changes missing from the migrations ×3; same migrations on all providers; content soft delete, recycle, restore
  and permanent delete defaults; content save (slug, unique URL); user delete; `BlockListState`; property-editor
  type names; appsettings key parity.
- Not built yet, add with the phase that needs them: external sign-in (ZA-002, Phase 4), draft save leaves blocks
  unpublished (ZA-038, needs bUnit or the block list redesign), tests on real PostgreSQL/SQL Server/Redis (Phase 6),
  template smoke test (Phase 11). The CI test step has not run on GitHub yet (CI only runs for `main-static`).

### Phase 3 — Broken admin actions (small, high impact)
ZA-001 (recycle-bin restore / final delete / open), ZA-045 + ZA-088 (delete user), ZA-076 (Culture & Domains false
error), ZA-103 (email confirmation), ZA-058 (layout re-render wipes edits), ZA-081 (silent context-menu failures).

### Phase 4 — Access control & identity
ZA-053 (restricted media public when recycled / case bypass), ZA-052 (`refreshsignin`), ZA-002 (external sign-up —
needs a live provider test), ZA-059 (lockout), ZA-019 + ZA-020 (SSRF / dynamic LINQ), ZA-003 (host-header links),
ZA-060 (proxy headers, IP allow-list in middleware), ZA-132 (fail-closed `/admin/**` authorisation, service role
checks, redirect-target validation), ZA-021/ZA-022/ZA-005 (sanitiser, uploads, SVG — some need
owner decisions; ask).

### Phase 5 — Content integrity
ZA-046 (duplicate URL breaks front-end), ZA-047 (drafts discarded), ZA-048 (recycle parent/children),
ZA-049 (copy shares draft), ZA-065, ZA-066, ZA-067 (publish/slug/nested-URL leaks), ZA-070, ZA-074, ZA-075, ZA-079,
ZA-016, ZA-135 (rich-text content links rewritten to relative URLs).

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
Retarget to `net11.0`, move Microsoft/EF/Npgsql packages to 11.x, Serilog.AspNetCore 11, **ZauberCMS.RTE 4** (adds
embeds — then add the CMS render step that expands embed placeholders on the public site, closing ZA-134), full retest on SQLite, SQL Server, PostgreSQL and
Redis, then release planning with the owner.

Owner instructions for this phase (2026-11-25):
- **Read the release notes first.** Before this move, and before every future .NET version change, read the
  official "What's new" / release notes and breaking changes for .NET, ASP.NET Core, Blazor, EF Core and C# of that
  version, and write a short summary of what affects ZauberCMS into this file before changing any code.
- **Investigate the async change.** The owner understands a new .NET release makes async work happen in the runtime
  "by default" (believed to be the runtime-async feature), which may change how async code should be written here.
  Establish from the release notes what it actually is, whether it is on by default in .NET 11, and what (if
  anything) should change in this codebase. Do not remove `async`/`await` on an assumption — confirm first.
- **Hot reload.** Check that hot reload works well for ZauberCMS in the dev site and in a template-built site
  (Razor, C# and CSS edits, admin and front-end), and fix what stops it.

## Open questions for the owner

- Release the ZA-044 fix as a hotfix from `main-static` now? (fixed on `rework-2026` only; 4.1.3 is still exposed)
- Installer scope (Phase 7).
- SVG uploads, sanitiser policy for `iframe`/`data:`, CAPTCHA/registration switch (Phase 4).
- Is a v5 major acceptable as the vehicle for the breaking changes in Phase 11?
