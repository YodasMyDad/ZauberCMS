# ZauberCMS Audit Report

Audited commit: 5668365 · Date: 2026-11-25 · Scope: full · Mode: report
Build: PASS · Warnings: 70 (all NuGet vulnerability advisories; 0 compiler; no previous baseline) · Vulnerable packages: 4 · Outdated majors: 5
Next free IDs: ZA-164 (findings) · ZP-001 (proposals) · Umbraco reference: not yet compared (first run with lenses G and H to come)
After Roadmap Phase 10d (2026-11-27): Build PASS · Warnings: 0 · Vulnerable packages: 0 · Tests: 912 (911 pass, 1 skipped for open finding ZA-118), SQL Server (LocalDB), PostgreSQL and Redis; `/audit changed` run on the phase (two reviewers, nothing High: the Medium — Radzen dialogs outside every error boundary — and the Lows fixed; the bins reloading on any save noted). ZA-025, ZA-085, ZA-096, ZA-097, ZA-101, ZA-108, ZA-156, ZA-161 fixed; ZA-026 and ZA-160 resolved. Next free ID: ZA-164

> First full run. All findings come from reading code plus the deterministic checks below; **nothing was run in a
> browser or against PostgreSQL/Redis**, so items marked as needing a repro are reasoned, not observed. Critical and
> most High findings were re-read by the auditor before inclusion. No code was changed (report mode); five wrong
> claims in `AGENTS.md` / `Docs/` found by this run were corrected.

## Summary

| Severity | Open | Fixed this run | Resolved since last run |
| --- | --- | --- | --- |
| Critical | 0 | 1 | 0 |
| High | 0 | 30 | 0 |
| Medium | 8 | 65 | 1 |
| Low | 30 | 23 | 5 |

**Work order and owner decisions: [Docs/Roadmap.md](../Docs/Roadmap.md).** Every open finding is assigned there to
a phase (10e, 11) or to its Backlog (ZA-128, ZA-129, ZA-151, ZA-155, ZA-163). What each phase fixed and how it was
verified: [history.md](history.md); the earlier after-phase status lines are in its run log.

## Open findings

### ZA-087 [Medium/High] Components package leaks npm manifests into consumer projects

- Where: `ZauberCMS.Components/ZauberCMS.Components.csproj:73-76` · Category: F · Since: 2026-11-25
- Issue / Trigger: `package.json` and `package-lock.json` ship as content files; the existing exclusion uses a stale machine-specific path.
- Fix: `Content Remove="package*.json"` + `Pack="false"`. · Blocked by: nothing.

### ZA-089 [Medium/High] CI can publish one version built from two commits

- Where: `.github/workflows/ci.yml:162-188` · Category: F · Since: 2026-11-25
- Issue / Trigger: Packages are pushed in arbitrary order with `--skip-duplicate` and the tag is created afterwards; a partial failure followed by a new commit reuses the version.
- Fix: push in dependency order; reserve the tag first. · Blocked by: nothing (CI only).

### ZA-090 [Medium/High] Unwired MVC rendering path ships as public API

- Where: `ZauberCMS.Core/ZauberSetup.cs:135,295,372-377,554-564`, `ZauberCMS.Routing/ZauberCMS.Routing.csproj:17` · Category: F · Since: 2026-11-25
- Issue / Trigger: `ZauberRouteValueTransformer`, `ContentFinderPipeline`, `Views/ZauberRender/*`, view components and `Rendering/*` are registered or shipped but never mapped.
- Fix: `[Obsolete]` now, remove in the next major; register memory cache explicitly first. · Blocked by: public API.

### ZA-091 [Medium/High] Core's public contracts depend on Radzen and Blazored.Modal types

- Where: `ZauberCMS.Core/Content/Interfaces/IContentProperty.cs:18`, `ZauberCMS.Core/Sections/Interfaces/ISectionNavGroupAction.cs:14` · Category: F · Since: 2026-11-25
- Issue / Trigger: A Radzen major upgrade is a binary break of Core's discovery interfaces (Radzen is three majors behind — ZA-130), and Core can't run headless.
- Fix: for the next major, move UI-typed interfaces and extensions to Components. · Blocked by: public API (major only).

### ZA-092 [Medium/High] Core exposes 362 public types and no internal ones

- Where: `ZauberCMS.Core/Plugins/AssemblyManager.cs:10-12,23` · Category: F · Since: 2026-11-25
- Issue / Trigger: Implementations, mappings, factories, middleware and unused public mutable statics are all frozen by the compatibility rule.
- Fix: make non-contract types internal before the next major. · Blocked by: public API.
- Reconciled 2026-11-26 (Phase 7): the installer and form protection added 28 more public types (`ZauberCMS.Core.Installer`, `ZauberCMS.Core.Security`: 22 in `.cs` files and six Razor components); "no internal ones" no longer holds — `InstallerDatabase` and `InstallerAssets` are internal.

### ZA-094 [Medium/Medium] Template pins an SDK-internal asset package at 10.0.0

- Where: `ZauberCMS.Template/template/ZauberCMSTemplate.Site/ZauberCMSTemplate.Site.csproj:11-14` · Category: F · Since: 2026-11-25
- Issue / Trigger: `Microsoft.AspNetCore.App.Internal.Assets` is hard-pinned and never stamped by CI, so new sites serve a stale `blazor.web.js`.
- Fix: `<RequiresAspNetWebAssets>true</RequiresAspNetWebAssets>`; template smoke test. · Blocked by: nothing.

### ZA-095 [Medium/High] Admin and reserved-path detection is a bare prefix match

- Where: `ZauberCMS/Components/App.razor:64-73`, `ZauberCMS.Core/Middleware/CultureMiddleware.cs:42-44`, `ZauberCMS.Core/Middleware/RedirectMiddleware.cs:34-36` · Category: E · Since: 2026-11-25
- Issue / Trigger: A page at `/administration` renders as admin (interactive circuit, Radzen assets) for visitors; `/accounting` or `/api-docs` skip culture and redirect handling.
- Fix: match on a segment boundary via one shared helper. · Blocked by: nothing.

### ZA-098 [Medium/High] No working front-end error page

- Where: `ZauberCMS.Template/template/ZauberCMSTemplate.Site/Program.cs:13`, `ZauberCMS.Web/Program.cs:13` · Category: E · Since: 2026-11-25
- Issue / Trigger: The template ships the exception handler commented out and setup adds none: production errors return an empty 500.
- Fix: ship an error route and wire the handler in setup. · Blocked by: pipeline order; new setting.

### ZA-030 [Low/Medium] Logout via top-level GET navigation still possible

- Where: `ZauberCMS.Components/Controllers/AuthController.cs` · Category: D

### ZA-031 [Low/High] MD5 for username/gravatar hashing

- Where: `ZauberCMS.Core/Extensions/StringExtensions.cs:315`, `Membership/ZauberSignInManager.cs:168` · Category: D

### ZA-032 [Low/High] Placeholder secrets in packaged appsettings

- Where: `ZauberCMS/appsettings.json:61,70,81` · Category: D
- Reconciled 2026-11-25: placeholders are now at lines 68-69 in all three appsettings files.

### ZA-033 [Low/Medium] Soft-deleted users keep username/email unique-index slots and PII

- Where: `MembershipService.cs:371-376` · Category: C/D · Issue: same email may be unable to re-register; GDPR erase path absent.
- Reconciled 2026-11-25: reachable from the admin UI since user delete was fixed (ZA-045, Roadmap Phase 3); no restore or purge UI exists.

### ZA-104 [Low/High] Image-resize cache is unbounded and upscaling is on

- Where: `ZauberCMS.Core/Settings/ZauberSettings.cs:100,112` · Category: D · Since: 2026-11-25
- Issue / Fix: Anonymous size/quality variations can fill the disk; default `AllowUpscale=false` and a finite `MaxCacheBytes`.

### ZA-105 [Low/High] Public `ExternalLoginAsync` skips the verified-email and `CanSignIn` gates

- Where: `ZauberCMS.Core/Membership/Services/MembershipService.cs:841-860` · Category: D · Since: 2026-11-25
- Issue / Fix: Unused by shipped pages; remove the fallback or mark `[Obsolete]`.
- Reconciled 2026-11-26 (Phase 7): the fallback now refuses when `GlobalSettings.AllowRegistration` is off (`:835-839`); the rest of the finding stands.

### ZA-106 [Low/Medium] Login reveals unconfirmed accounts and re-sends confirmation emails

- Where: `ZauberCMS.Core/Membership/Services/MembershipService.cs:626-640` · Category: D · Since: 2026-11-25
- Issue / Fix: Generic message; throttle resends.

### ZA-107 [Low/High] Email addresses written to logs

- Where: `ZauberCMS.Core/Membership/Services/MembershipService.cs:664,685,795,1001,1086`, `ZauberCMS.Core/Installer/Services/InstallService.cs` (`RecoverAdminAsync`) · Category: D · Since: 2026-11-25
- Issue / Fix: Log the user id instead.
- Reconciled 2026-11-26 (Phase 7): `InstallService.RecoverAdminAsync` logs the user name of the account named by `Zauber:Identity:RecoverAdminUser` (not the configured value). The `ZauberSignInManager.cs:76` log cited before (unverified email) was removed in Phase 4.

### ZA-109 [Low/High] CI and release script hardening

- Where: `.github/workflows/ci.yml:3-7,10-27,83-84,155`, `release.ps1:58` · Category: D/F · Since: 2026-11-25
- Issue / Fix: No explicit `permissions` on the build job, actions pinned by tag, docs-only commits publish (no `paths-ignore`), prerelease versions break the patch parse, `Set-Content` adds a blank line per run.

### ZA-110 [Low/Low] Restricted-content gate relies on `NavigateTo` throwing

- Where: `ZauberCMS.Components/Pages/EntryPage.razor:90,97` · Category: D · Since: 2026-11-25
- Issue / Fix: Clear the view and return after redirecting.

### ZA-111 [Low/High] Hosted services and middleware ignore cancellation

- Where: `ZauberCMS.Core/Jobs/DailyJob.cs:22-52`, `ZauberCMS.Components/Seo/Startup/SitemapGeneratorService.cs:34,44,115`, `ZauberCMS.Core/Middleware/CultureMiddleware.cs:56` · Category: A · Since: 2026-11-25
- Issue / Fix: `BackgroundService` + `PeriodicTimer`; forward `RequestAborted`.

### ZA-114 [Low/High] Layouts set process-wide culture; sitemap formats with current culture and drops the home page

- Where: `ZauberCMS.Components/Admin/Layout/SectionLayout.razor:81-85`, `ZauberCMS.Components/Pages/BlankLayout.razor:29-33`, `ZauberCMS.Components/Seo/Startup/SitemapGeneratorService.cs:74-77,180-181` · Category: A/C · Since: 2026-11-25
- Issue / Fix: Remove `DefaultThreadCurrentCulture` writes; invariant culture; treat `Guid.Empty` redirect as null.

### ZA-115 [Low/High] Smaller performance items

- Where: `ZauberCMS.Core/Middleware/RedirectMiddleware.cs:16-17,88-92`, `ZauberCMS.Core/Extensions/StringExtensions.cs:300,374,390,405`, `ZauberCMS.Core/Data/Services/DataService.cs:140-150`, `ZauberCMS.Core/Content/Services/ContentService.cs:2157-2159,2261-2269` · Category: B · Since: 2026-11-25
- Issue / Fix: Redirect cache not invalidated on save (5 min, 5000 cap); regex built per call; paging without a stable order; `First` without `OrderBy`; a query per slug candidate; no cache stampede guard.

### ZA-116 [Low/High] Redirect rules containing "?" are treated as regex; no loop guard

- Where: `ZauberCMS.Core/Middleware/RedirectMiddleware.cs:140-150` · Category: C · Since: 2026-11-25
- Issue / Fix: Explicit "is regex" flag (schema).

### ZA-118 [Low/High] Packaged `appsettings.json` has no `Zauber:ImageResize` section

- Where: `ZauberCMS/appsettings.json:88` (end of `Plugins`; Web and template continue with `ImageResize` at `:89`) · Category: F · Since: 2026-11-25
- Issue / Fix: Web and template have it; behaviour identical via code defaults. Add it and a key-parity test.

### ZA-119 [Low/High] Package metadata gaps and csproj cruft

- Where: `ZauberCMS/ZauberCMS.csproj:83`, `ZauberCMS.Components/ZauberCMS.Components.csproj:19,34-41` · Category: F · Since: 2026-11-25
- Issue / Fix: No `PackageReadmeFile`, icon, SourceLink or symbols; project-wide `NoWarn CS0618` hides obsolete usage; duplicated transitive references.

### ZA-120 [Low/High] Discovery and configuration fail silently or late

- Where: `ZauberCMS.Core/Plugins/ExtensionManager.cs:43,68,192,203`, `ZauberCMS.Core/ZauberSetup.cs:83-85` · Category: F · Since: 2026-11-25
- Issue / Fix: Bootstrap skips are not logged; type cache keyed by `T` alone; mistyped `Zauber:Plugins:*` throws `KeyNotFoundException`; no options validation.

### ZA-121 [Low/High] DI leftovers

- Where: `ZauberCMS.Core/ZauberSetup.cs:109-116,223,226,287` · Category: F · Since: 2026-11-25
- Issue / Fix: Unused scoped `HttpClient` and `IUserEmailStore`; `SignInManager` registered twice; unused `Actions` on `ContentNavGroup`.

### ZA-123 [Low/High] Template and sample rough edges

- Where: `ZauberCMS.Template/template/ZauberCMSTemplate.Site/Properties/launchSettings.json:19`, `ZauberCMS.Web/Program.cs:13` · Category: F · Since: 2026-11-25
- Issue / Fix: Launch profile named "Umbraco.Web.UI"; no template `.gitignore`; no starter `_Imports`, layout or view; sample points at a non-existent `/Home/Error`.
- Reconciled 2026-11-26 (Phase 7): the starter kit added `_Imports.razor`, `Layouts/MainLayout.razor` and two views (`Views/HomeView.razor`, `Views/TextPageView.razor`) to the template, so that part is resolved. Still open: the "Umbraco.Web.UI" profile name, no template `.gitignore`, and `/Home/Error` (the sample's `ZauberCMS.Web/Program.cs:13` uses it and no such route exists; the template's `Program.cs:13` has the same line commented out).

### ZA-124 [Low/High] API convention drift to settle before the next major

- Where: `ZauberCMS.Core/Content/Parameters/QueryContentParameters.cs:19`, `ZauberCMS.Core/Extensions/AuditExtensions.cs:9` · Category: F · Since: 2026-11-25
- Issue / Fix: `IsDeleted` vs `IncludeDeleted`; `Cached` defaults differ across parameter classes; `CancellationToken?`; discovery interfaces scattered across namespaces.

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

### ZA-131 [Low/High] Engineering baseline still absent (roll-up)

- Where: repo root · Category: F · Since: 2026-11-25
- Issue / Fix: No `Directory.Build.props` for the product projects, central package management, `.editorconfig`, SDK pin in `global.json` or `.gitattributes`; CI runs no vulnerability scan.
- Progress 2026-11-25 (Roadmap Phase 2): test projects `tests/ZauberCMS.Core.Tests` and `tests/ZauberCMS.Components.Tests` exist and CI runs them (54 tests, 4 skipped for open findings ZA-011, ZA-036, ZA-045, ZA-118). `global.json` exists but only selects the test runner. Known deviations in `Docs/Standards.md` all still present (≈76 scope-per-method sites, 11 sync paging methods, sync seeding).

### ZA-149 [Low/High] The login and registration rate limits are fixed in code
- Where: `ZauberCMS.Core/ZauberSetup.cs` (`AddRateLimiter`: policies `"login"` and `"register"`) · Category: F · Since: 2026-11-26 (Phase 7 audit)
- Issue / Trigger: 10 requests per address in 5 minutes for login-like pages, 5 POSTs in 10 minutes for
  registration, counting refused attempts. A site cannot change them (a second `AddPolicy` with the same name
  throws), and behind a proxy without `Zauber:ForwardedHeaders` every visitor shares one allowance.
- Fix: bind the limits to `Zauber:Identity` settings with today's values as defaults. · Blocked by: nothing.

### ZA-150 [Low/High] A form-protection token can be used again for 12 hours
- Where: `ZauberCMS.Core/Security/Services/FormProtectionService.cs` · Category: D · Since: 2026-11-26 (Phase 7 audit)
- Issue / Trigger: the token is a signed timestamp, not single-use and not tied to a visitor. A script written
  for the site fetches one token, waits out the minimum fill time once and reuses it; only the rate limit and a
  CAPTCHA stop it. Documented in `Docs/Installer.md` as the limit of these checks.
- Fix: add a random id to the token and remember used ids until they expire (per server unless stored in shared
  state). · Blocked by: `IFormProtectionService` is public API from v5 on; decide before release.

### ZA-151 [Low/Medium] The installer's pages cannot be restyled or extended from a site
- Where: `ZauberCMS.Core/Installer/Middleware/InstallerMiddleware.cs` (renders `InstallerPage` directly), `ZauberCMS.Core/Installer/Components/` · Category: F · Since: 2026-11-26 (Phase 7 audit)
- Issue / Trigger: `IInstallService` can be replaced and `InstallService`'s methods are virtual, but the pages,
  their steps and their styling are fixed. An agency cannot brand the installer or add a question, which sits
  badly with "replaceable defaults".
- Fix: a replaceable page component or a discovered "installer step" interface. · Blocked by: owner decision
  (Roadmap, open questions).

### ZA-155 [Low/High] Resized copies of a deleted image stay in `_mediacache`
- Where: `ZauberCMS.Core/Media/Services/MediaService.cs` (`PurgeAsync`), ImageResize cache root (`ZauberSetup.cs`, `_mediacache`) · Category: D · Since: 2026-11-26 (Phase 8 audit)
- Issue / Trigger: a permanent media delete removes the original file, not the resized copies ImageResize made of
  it. Their names are hashes of path, options and file date, so they are hard to guess, but a "delete it for
  good" request (GDPR) is not fully met. Documented in `Docs/Services.md`.
- Fix: remove the cache entries for the file on purge (needs an ImageResize API for it) or prune the cache. ·
  Blocked by: the ImageResize package.

### ZA-163 [Low/High] After-save hooks run before the commit
- Where: `ZauberCMS.Core/Extensions/DbContextExtensions.cs` (`SaveChangesAndLog`), `MediaService.PurgeAsync` · Category: F · Since: 2026-11-26 (Phase 10b)
- Issue / Trigger: `IAfterEntitySave<T>` runs once the rows are written, inside the caller's transaction. A change
  rolled back after that (a media purge whose file can't be removed) was already reported to the hooks as done; a
  plugin mirroring media elsewhere removes a file that is back. Umbraco raises its "-ed" notifications when the scope
  completes.
- Fix: an after-commit point in the hook API. · Blocked by: public API (owner decision; Roadmap Backlog).

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
| Config parity | Packaged `ZauberCMS/appsettings.json` lacks `Zauber:ImageResize` (12 keys) → ZA-118 |
| Docs drift | Paths all resolve; ~55 claims spot-checked, 5 wrong, corrected this run |
| Repo hygiene | Only `ZauberCMS.Web/app.db` tracked (exempt); no `.gitattributes` → ZA-131 |
| Tests | Not part of the first run. After Phase 10d: 912 (911 pass, 1 skipped for ZA-118), SQL Server (LocalDB), PostgreSQL and Redis |
| Pattern counts | Not counted yet (added to the audit 2026-11-27); the next run records the first counts |
| Hot-path budget | First run, by reading: about 10 round-trips cold; 0 within 5 s of the same URL; 4–5 after 5 s idle; 2 per 404; +3 per block list, +1 per single media picker. Phase 10 changed front-end caching since: re-measure |
| Package contents | Not checked yet (ZA-087 came from reading the csproj) |
| Template smoke test | Not run yet (Standards §9: not built) |

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
