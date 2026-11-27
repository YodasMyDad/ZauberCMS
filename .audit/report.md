# ZauberCMS Audit Report

Last full run: 5668365 (2026-11-25, full) · Last changed run: b001147+wt (2026-11-27, Roadmap Phase 10e) · Mode: report
Build: PASS · Warnings: 70 (all NuGet vulnerability advisories; 0 compiler; no previous baseline) · Vulnerable packages: 4 · Outdated majors: 5
Next free IDs: ZA-169 (findings) · ZP-001 (proposals) · Umbraco reference: not yet compared (first run with lenses G and H to come)
After Roadmap Phase 11f (2026-11-27): Build PASS · Warnings: 0 · Vulnerable packages: 0 · Tests: 1109 (all pass, none skipped), SQL Server (LocalDB), PostgreSQL and Redis · Template smoke test passes; `doublecheck` self-review on the part. ZA-092, ZA-124 fixed. Next free ID: ZA-169. Earlier after-phase lines: [history.md](history.md) run log

> First full run. All findings come from reading code plus the deterministic checks below; **nothing was run in a
> browser or against PostgreSQL/Redis**, so items marked as needing a repro are reasoned, not observed. Critical and
> most High findings were re-read by the auditor before inclusion. No code was changed (report mode); five wrong
> claims in `AGENTS.md` / `Docs/` found by this run were corrected.

## Summary

| Severity | Open | Fixed this run | Resolved since last run |
| --- | --- | --- | --- |
| Critical | 0 | 1 | 0 |
| High | 0 | 30 | 0 |
| Medium | 0 | 71 | 1 |
| Low | 6 | 44 | 6 |

**Work order and owner decisions: [Docs/Roadmap.md](../Docs/Roadmap.md).** Every open finding is assigned there to
a phase (10f: ZA-155, ZA-163; 10h: ZA-129; 10i: ZA-128; 11g: ZA-125, ZA-126); there is no Backlog (owner, 2026-11-27).
ZA-151 was declined (D39). What each phase fixed and how it was
verified: [history.md](history.md); the earlier after-phase status lines are in its run log.

## Open findings

### ZA-125 [Low/High] Very large classes; `Content.Url` is obsolete with no exit

- Where: `ZauberCMS.Core/Content/Services/ContentService.cs` (2538 lines), `ZauberCMS.Core/Membership/Services/MembershipService.cs` (1091), `ZauberCMS.Core/Content/Models/Content.cs:22` · Category: F · Since: 2026-11-25
- Issue / Fix: Split by concern; give the obsolete member a removal plan (16 pragma suppressions).

### ZA-126 [Low/Medium] Interactive custom components receive the whole `Content` graph

- Where: `ZauberCMS.Components/ContentComponents/RenderCustomContent.razor:23-44` · Category: E · Since: 2026-11-25
- Issue / Fix: Serialised into the page across the SSR boundary; pass an id or DTO (public interface).

### ZA-128 [Low/High] Accessibility roll-up

- Where: `ZauberCMS.Components/Shared/TreeButton.razor:4`, `ZauberCMS.Components/Account/Pages/Login.razor:28-34` · Category: E · Since: 2026-11-25
- Issue / Fix: 8 of 38 icon buttons unlabelled; 7 clickable divs with no role or key handler (every tree button); ~139 Radzen inputs vs ~75 labels; `label for` without matching ids.

### ZA-129 [Low/High] Admin UI is not localised

- Where: `ZauberCMS.Components/` (166 razor files) · Category: E · Since: 2026-11-25
- Issue / Fix: 0 localiser uses, ~370 literal strings; `AdminDefaultLanguage` only affects formatting.

### ZA-155 [Low/High] Resized copies of a deleted image stay in `_mediacache`
- Where: `ZauberCMS.Core/Media/Services/MediaService.cs` (`PurgeAsync`), ImageResize cache root (`ZauberSetup.cs`, `_mediacache`) · Category: D · Since: 2026-11-26 (Phase 8 audit)
- Issue / Trigger: a permanent media delete removes the original file, not the resized copies ImageResize made of
  it. Their names are hashes of path, options and file date, so they are hard to guess, but a "delete it for
  good" request (GDPR) is not fully met. Documented in `Docs/Services.md`.
- Fix: remove the cache entries for the file on purge (needs an ImageResize API for it) or prune the cache. ·
  Blocked by: nothing (Roadmap Phase 10f).

### ZA-163 [Low/High] After-save hooks run before the commit
- Where: `ZauberCMS.Core/Extensions/DbContextExtensions.cs` (`SaveChangesAndLog`), `MediaService.PurgeAsync` · Category: F · Since: 2026-11-26 (Phase 10b)
- Issue / Trigger: `IAfterEntitySave<T>` runs once the rows are written, inside the caller's transaction. A change
  rolled back after that (a media purge whose file can't be removed) was already reported to the hooks as done; a
  plugin mirroring media elsewhere removes a file that is back. Umbraco raises its "-ed" notifications when the scope
  completes.
- Fix: an after-commit point in the hook API. · Blocked by: nothing (Roadmap Phase 10f).

## Improvement proposals

None recorded yet: lenses G (editor UX) and H (Umbraco alignment) were added to the audit on 2026-11-27 and have
not run. The next `full` run (or `/audit G,H`) fills this section.

## Fixed this run / Resolved since last run

Moved to [history.md](history.md) on 2026-11-27. From now on this section lists only the current run's items;
history.md keeps every run.

## Deterministic checks

| Check | Result |
| --- | --- |
| Build (`dotnet build ZauberCMS.sln -c Release`) | PASS, 0 errors, 70 warnings — all `NU1902`/`NU1903`; no compiler warnings (but `NoWarn CS0618` hides obsolete usage in Components) |
| Vulnerable packages | 4 — HtmlSanitizer 9.0.892 (High, direct), AngleSharp 0.17.1 (Moderate), SQLitePCLRaw.lib.e_sqlite3 2.1.11 (High), System.Security.Cryptography.Pkcs 10.0.0 (High + Moderate) → ZA-102 |
| Outdated packages | Microsoft.* 10.0.7 → 10.0.15; Npgsql EF 10.0.1 → 10.0.4; majors: Radzen 13.3.0, MailKit 5.0.0, HtmlSanitizer 11.0.26, ZauberCMS.RTE 3.0.0, Serilog.AspNetCore 11.0.0 (and .NET 11); Blazored.Modal "Not found at the sources" → ZA-130 |
| Version alignment | OK — five packages at 4.1.3, chain consistent, shared dependency versions aligned |
| Migration parity | OK — 3 migrations per provider, same names, snapshots aligned (52 entities / 53 indexes / 30 FKs). Content defect: ZA-050 |
| Config parity | Since Phase 11c the three `appsettings.json` files have the same `Zauber` keys and `Serilog` section (ZA-118, ZA-168; `AppSettingsParityTests`) |
| Docs drift | Paths all resolve; ~55 claims spot-checked, 5 wrong, corrected this run |
| Repo hygiene | Only `ZauberCMS.Web/app.db` tracked (exempt); `.gitattributes`, `.editorconfig`, `Directory.Build.props`, `Directory.Packages.props` and an SDK pin since Phase 11b (ZA-131 fixed) |
| Tests | Not part of the first run. After Phase 11c: 1034 (all pass, none skipped), SQL Server (LocalDB), PostgreSQL and Redis |
| Pattern counts | Not counted yet (added to the audit 2026-11-27); the next run records the first counts |
| Hot-path budget | First run, by reading: about 10 round-trips cold; 0 within 5 s of the same URL; 4–5 after 5 s idle; 2 per 404; +3 per block list, +1 per single media picker. Phase 10 changed front-end caching since: re-measure |
| Package contents | Phase 11c: all five packed before and after and compared (file lists, nuspecs); see the Roadmap's 11c notes |
| Template smoke test | `tests/template-smoke.ps1` since Phase 11c, in CI's build job; passes |

## Coverage

- Reviewed: all six categories (A async/lifetimes, B data/performance, C correctness, D security, E Blazor/render
  modes, F architecture/packaging) across Core, Components, Routing, ZauberCMS, the template and CI.
- Front-end hot path measured by reading: about 10 DB round-trips cold; 0 within 5 s of the same URL; 4-5 after 5 s
  idle; 2 per 404; plus 3 per block list and 1 per single media picker on every request.
- Not covered by the first run (the next `full` run must cover what later phases have not: SKILL.md §0 step 6): any
  runtime verification (browser, PostgreSQL, Redis, proxy); `app.db` contents; internals of the
  ImageResize, ZauberCMS.RTE, TinyMCE and Monaco packages; `wwwroot` JavaScript beyond a sink grep; full bodies of
  `SeoService`, `TagService`, `LanguageService`; per-editor settings round-trip (only Numeric and ContentPicker read);
  `ContentTypeEditor`, compositions, and the Sort/Move dialogs; double-submit and re-entrancy in admin dialogs;
  focus and keyboard behaviour in side panels; Radzen 10 obsolete API usage (hidden by `NoWarn`).
- Runtime checks done in a browser so far (scratch SQLite database): the Phase 1 package/editor smoke test and the
  Phase 3 admin actions and the Phase 4 checks listed in [history.md](history.md). Phase 6: the app on PostgreSQL +
  Redis containers (see Phase 6 in history.md); the test suite runs on PostgreSQL and Redis, and on SQL Server when
  `ZAUBER_TEST_SQLSERVER` is set (run locally: 78 cases pass; the app also run on SQL Server). Still nothing
  behind a proxy.
- Tests added in Roadmap Phase 4: restricted media middleware; lockout; refresh ticket; external sign-up claims;
  grid filter/sort guard; SEO outbound client; site URL links; admin access middleware and IP allow-list;
  forwarded-headers options; redirect targets; sanitiser; upload content check; SVG sanitiser.
- Tests added in Roadmap Phase 3: email confirmation and change of email; delete by the obsolete `UserId`;
  data-grid soft delete; outdated-copy save refused; language-only save.
- Tests now exist for (Roadmap Phase 2, 2026-11-25): soft-delete Get/Query defaults, recycle, restore and permanent
  delete at service level; user delete; container validation ×3 providers; no pending model changes ×3; empty SQLite
  database migrates; `BlockListState`; property-editor type names; appsettings key parity. Still untested from the
  list below: the recycle-bin context menus themselves (ZA-001 is in Components), external sign-in (ZA-002), draft
  save and blocks (ZA-038), ZA-037, PostgreSQL migration on a real server (ZA-050), template smoke test.
- Original list of highest-value first tests: recycle-bin restore/final delete (ZA-001); soft-delete query defaults
  (ZA-016); external sign-in with and without a verified-email claim (ZA-002); user delete persists (ZA-045, ZA-088);
  DI container validation for all three providers (ZA-011); `BlockListState` units (ZA-034…ZA-037); draft save leaves
  blocks unpublished (ZA-038); no pending model changes ×3 and an empty database migrates (ZA-050); discovery
  snapshot by type name; template smoke test with appsettings key parity.
