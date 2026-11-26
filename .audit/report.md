# ZauberCMS Audit Report

Audited commit: 5668365 · Date: 2026-11-25 · Scope: full · Mode: report
Build: PASS · Warnings: 70 (all NuGet vulnerability advisories; 0 compiler; no previous baseline) · Vulnerable packages: 4 · Outdated majors: 5
After Roadmap Phase 10 (2026-11-26): Build PASS · Warnings: 0 · Tests: 828 (827 pass, 1 skipped for open finding ZA-118), SQL Server (LocalDB), PostgreSQL and Redis; `/audit changed` run on the phase (two reviewers: their High and Mediums fixed — tree flags wiped by the bin trees, a front-end cache that never expired on busy pages or kept misses — the rest fixed or recorded as ZA-161). Next free ID: ZA-162
After Roadmap Phase 9 (2026-11-26): Build PASS · Warnings: 0 · Tests: 768 (766 pass, 2 skipped for open findings ZA-011, ZA-118), SQL Server (LocalDB), PostgreSQL and Redis; `/audit changed` run on the phase (two reviewers and a git-history search). Next free ID: ZA-160
After Roadmap Phase 8 (2026-11-26): Build PASS · Warnings: 0 · Tests: 700 (697 pass, 3 skipped for open findings ZA-011, ZA-036, ZA-118), all SQL Server variants run for the first time (LocalDB via `ZAUBER_TEST_SQLSERVER`), PostgreSQL and Redis in Docker; `/audit changed` run on the phase (two reviewers). Next free ID: ZA-158
After Roadmap Phase 7 (2026-11-26): Build PASS · Warnings: 0 · Vulnerable packages: 0 · Tests: 628 (528 pass, 100 skipped: 97 SQL Server variants without `ZAUBER_TEST_SQLSERVER`, 3 for open findings ZA-011, ZA-036, ZA-118), PostgreSQL and Redis tests in Docker; `/audit changed` run on the phase (three reviewers). Next free ID: ZA-153
After Roadmap Phase 6 (2026-11-25): Build PASS · Warnings: 0 · Tests: 428 (425 pass, 3 skipped for open findings ZA-011, ZA-036, ZA-118), PostgreSQL and Redis tests in Docker; `/audit changed` run on the phase
After Roadmap Phase 5 (2026-11-25): Build PASS · Warnings: 0 · Tests: 315 (312 pass, 3 skipped for open findings ZA-011, ZA-036, ZA-118); `/audit changed` run on the phase
After the Phase 4 follow-ups (2026-11-25, RTE 4.0.0): Build PASS · Warnings: 0 · Tests: 259 (256 pass, 3 skipped for open findings ZA-011, ZA-036, ZA-118)
After Roadmap Phase 4 (2026-11-25): Build PASS · Warnings: 0 · Tests: 231 (228 pass, 3 skipped for open findings ZA-011, ZA-036, ZA-118)
After Roadmap Phase 3 (2026-11-25): Build PASS · Warnings: 0 · Tests: 65 (62 pass, 3 skipped for open findings ZA-011, ZA-036, ZA-118)
After Roadmap Phase 1 (2026-11-25, working tree): Build PASS · Warnings: 0 · Vulnerable packages: 0 · Outdated majors: 1 held on purpose (Serilog.AspNetCore 11, needs .NET 11 packages)

> First full run. All findings come from reading code plus the deterministic checks below; **nothing was run in a
> browser or against PostgreSQL/Redis**, so items marked as needing a repro are reasoned, not observed. Critical and
> most High findings were re-read by the auditor before inclusion. No code was changed (report mode); five wrong
> claims in `AGENTS.md` / `Docs/` found by this run were corrected.

## Summary

| Severity | Open | Fixed this run | Resolved since last run |
| --- | --- | --- | --- |
| Critical | 0 | 1 | 0 |
| High | 0 | 30 | 0 |
| Medium | 20 | 53 | 0 |
| Low | 49 | 7 | 1 |

**Work order and owner decisions: [Docs/Roadmap.md](../Docs/Roadmap.md).** Fixed so far: ZA-044 (Phase 0), ZA-102, ZA-130 and ZA-133 (Phase 1), ZA-001, ZA-045, ZA-058, ZA-076, ZA-081, ZA-088, ZA-103 and ZA-137 (Phase 3), ZA-002, ZA-003, ZA-005, ZA-019, ZA-020, ZA-021, ZA-022, ZA-052, ZA-053, ZA-059, ZA-060, ZA-132, ZA-138 and ZA-139 (Phase 4), ZA-134, ZA-136, ZA-141 and ZA-142 (Phase 4 follow-ups), ZA-004, ZA-023, ZA-024 and ZA-148 (Phase 7), ZA-006, ZA-013, ZA-153 and ZA-154 (Phase 8), ZA-008, ZA-009, ZA-010, ZA-034 … ZA-043 and ZA-158 (Phase 9), ZA-056, ZA-057, ZA-061 … ZA-064, ZA-069, ZA-071, ZA-073, ZA-083, ZA-084, ZA-054, ZA-068, ZA-082, ZA-080, ZA-086 and ZA-011 (Phase 10), ZA-016, ZA-046, ZA-047, ZA-048, ZA-049, ZA-065, ZA-066, ZA-067, ZA-070, ZA-074, ZA-075, ZA-079 and ZA-135 (Phase 5), ZA-007, ZA-014, ZA-017, ZA-050, ZA-051, ZA-055, ZA-072, ZA-122 and ZA-143 (Phase 6), committed on `rework-2026`. Next free ID: ZA-162.

## Open findings

### ZA-012 [Medium/High] `SaveChangesAndLog` hooks see `Unchanged` after audit save; ContentType veto can't roll back

- Where: `ContentService.cs:369,982-985` · Category: C
- Reconciled 2026-11-25: same pattern on the recycle path (`ContentService.cs:497-498`).
- Phase 9 (2026-11-26): content saves of values now go through the many-entity `SaveChangesAndLog` (hooks get Added/Modified and a veto stops the save). Still open for saves with `ExcludePropertyData` (sort, move, restore), the recycle path and ContentType saves.

### ZA-015 [Medium/High] `AddFluentValidation` never unsubscribes

- Where: `ZauberCMS.Core/Shared/Validation/EditContextFluentValidationExtensions.cs:22,25` · Category: A
- Reconciled 2026-11-25: same file also has unsynchronised static lists and `async` handlers with no try/catch (a throwing validator ends the circuit) — lines 12-13, 107-125.

### ZA-018 [Medium/Medium] `SaveTagItemAsync` bypasses `SaveChangesAndLog` hooks

- Where: `ZauberCMS.Core/Tags/Services/TagService.cs:222-224` · Category: C

### ZA-077 [Medium/High] Importing a content type over an existing alias orphans all its values

- Where: `ZauberCMS.Core/Content/Services/ContentService.cs:1791-1815,1737,1951` · Category: C · Since: 2026-11-25
- Issue / Trigger: Property and tab IDs are regenerated, so the next save drops unmatched values; content import is lossy too. Public API only — no admin caller.
- Fix: match existing properties by alias and keep IDs. · Blocked by: nothing.

### ZA-078 [Medium/High] Multi-pick order is lost on the front-end

- Where: `ZauberCMS.Core/Extensions/ContentExtensions.cs:208-209,244-262,285-293` · Category: C · Since: 2026-11-25
- Issue / Trigger: Media, content and user pickers return query order, not the editor's order (unlike `GetBlocks`).
- Fix: reorder by stored IDs. · Blocked by: visible order change.

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

### ZA-093 [Medium/Medium] Write services need a Blazor component scope

- Where: `ZauberCMS.Core/Content/Services/ContentService.cs:29`, `ZauberCMS.Core/Membership/Services/MembershipService.cs:35`, `ZauberCMS.Core/Media/Services/MediaService.cs:30` · Category: F · Since: 2026-11-25
- Issue / Trigger: Services inject `AuthenticationStateProvider` (27 uses), so save or delete can't be called from a controller, API or hosted service.
- Fix: `ICurrentUserAccessor` with an HttpContext fallback. · Blocked by: public constructors.
- Reconciled 2026-11-26 (Phase 7): the installer works around the same limitation by setting the authentication state for its own scope (`ZauberCMS.Core/Installer/Services/InstallService.cs:444-453`, `RunAsAsync`).

### ZA-094 [Medium/Medium] Template pins an SDK-internal asset package at 10.0.0

- Where: `ZauberCMS.Template/template/ZauberCMSTemplate.Site/ZauberCMSTemplate.Site.csproj:11-14` · Category: F · Since: 2026-11-25
- Issue / Trigger: `Microsoft.AspNetCore.App.Internal.Assets` is hard-pinned and never stamped by CI, so new sites serve a stale `blazor.web.js`.
- Fix: `<RequiresAspNetWebAssets>true</RequiresAspNetWebAssets>`; template smoke test. · Blocked by: nothing.

### ZA-095 [Medium/High] Admin and reserved-path detection is a bare prefix match

- Where: `ZauberCMS/Components/App.razor:64-73`, `ZauberCMS.Core/Middleware/CultureMiddleware.cs:42-44`, `ZauberCMS.Core/Middleware/RedirectMiddleware.cs:34-36` · Category: E · Since: 2026-11-25
- Issue / Trigger: A page at `/administration` renders as admin (interactive circuit, Radzen assets) for visitors; `/accounting` or `/api-docs` skip culture and redirect handling.
- Fix: match on a segment boundary via one shared helper. · Blocked by: nothing.

### ZA-096 [Medium/Medium] Admin scripts don't load when arriving by enhanced navigation

- Where: `ZauberCMS/Components/App.razor:32-41`, `ZauberCMS.Components/Pages/NewSite.razor:8` · Category: E · Since: 2026-11-25
- Issue / Trigger: Scripts inserted by enhanced navigation don't execute, so Radzen interop throws. Needs a browser check.
- Fix: `data-enhance-nav="false"` on links into admin. · Blocked by: nothing.

### ZA-097 [Medium/High] No `ErrorBoundary` anywhere

- Where: `ZauberCMS.Components/Admin/Layout/SectionLayout.razor:40-42`, `ZauberCMS.Components/Admin/DynamicContentProperty.razor:5`, `ZauberCMS.Components/ContentComponents/RenderBlock.razor:13` · Category: E · Since: 2026-11-25
- Issue / Trigger: One exception in any property editor, dashboard or consumer block preview ends the circuit and all unsaved work.
- Fix: boundaries around `@Body`, each property editor and each preview. · Blocked by: UX.

### ZA-098 [Medium/High] No working front-end error page

- Where: `ZauberCMS.Template/template/ZauberCMSTemplate.Site/Program.cs:13`, `ZauberCMS.Web/Program.cs:13` · Category: E · Since: 2026-11-25
- Issue / Trigger: The template ships the exception handler commented out and setup adds none: production errors return an empty 500.
- Fix: ship an error route and wire the handler in setup. · Blocked by: pipeline order; new setting.

### ZA-099 [Medium/High] Tags editor writes to the database on every render

- Where: `ZauberCMS.Components/Editors/TagsProperty.razor:47-64,78-83` · Category: E · Since: 2026-11-25
- Issue / Trigger: `SaveTagItemAsync` runs from `OnParametersSetAsync` (including prerender) and tag links persist before the content is saved — even for cancelled content.
- Fix: persist tags in the content save path. · Blocked by: tag sync behaviour.

### ZA-100 [Medium/High] Selecting files in more than one batch breaks the upload

- Where: `ZauberCMS.Components/Editors/MultipleMediaUpload.razor:183-199,236-242` · Category: E · Since: 2026-11-25
- Issue / Trigger: Files are accumulated across change events but earlier `IBrowserFile`s are invalidated: pick two, pick another, Save → earlier batch fails.
- Fix: upload per change event. · Blocked by: UX.

### ZA-101 [Medium/High] Admin prerenders, so every page initialises twice

- Where: `ZauberCMS/Components/App.razor:29,79-90` · Category: E · Since: 2026-11-25
- Issue / Trigger: Double loads and flicker; before ZA-058 was fixed (Roadmap Phase 3) some pages loaded four times.
- Fix: `InteractiveServerRenderMode(prerender: false)` for admin. · Blocked by: nothing now (the IP allow-list moved to middleware in Phase 4, ZA-060).

### ZA-025 [Low/High] Duplicate tree alias constant

- Where: `ZauberCMS.Core/Constants.cs:89` (`StructureElementTypeTree == "StructureContentTypeTree"`) · Category: C

### ZA-026 [Low/High] `<RadzenComponents/>` rendered twice

- Where: `ZauberCMS.Components/Admin/Layout/SectionLayout.razor:10,16` · Category: E

### ZA-027 [Low/Medium] Numeric editor stores values in current culture

- Where: `ZauberCMS.Components/Editors/NumericProperty.razor:41` · Category: C
- Reconciled 2026-11-25: read side (`:33`, `Convert.ToDouble`) is culture-dependent too.

### ZA-028 [Low/High] Duplicate property aliases throw in `ToDictionary`

- Where: `Content/Models/Content.cs:164`, `Membership/Models/User.cs:30` · Category: C

### ZA-029 [Low/High] ContentPicker stores single item as string, multiple as JSON

- Where: `ZauberCMS.Components/Editors/ContentPickerProperty.razor:146-148` · Category: C · Blocked by: existing data shapes.

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

### ZA-085 [Low/High] SEO checker blocks the circuit: sync HTTP, no timeout or cancellation

- Where: `ZauberCMS.Components/Seo/SeoChecker.razor:53-56`, `ZauberCMS.Core/Seo/Checks/BrokenLinksCheck.cs:54-70` · Category: A · Since: 2026-11-25
- Issue / Fix: Same lines as ZA-019; async load with timeout and a disposed `CancellationTokenSource`.

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

### ZA-108 [Low/High] Admin loads Sortable.js from a CDN without SRI; package scripts not fingerprinted

- Where: `ZauberCMS/Components/App.razor:21-22,36-43` · Category: D/E · Since: 2026-11-25
- Issue / Fix: Vendor locally or add `integrity`; use `@Assets[...]`.

### ZA-109 [Low/High] CI and release script hardening

- Where: `.github/workflows/ci.yml:3-7,10-27,83-84,155`, `release.ps1:58` · Category: D/F · Since: 2026-11-25
- Issue / Fix: No explicit `permissions` on the build job, actions pinned by tag, docs-only commits publish (no `paths-ignore`), prerelease versions break the patch parse, `Set-Content` adds a blank line per run.

### ZA-110 [Low/Low] Restricted-content gate relies on `NavigateTo` throwing

- Where: `ZauberCMS.Components/Pages/EntryPage.razor:90,97` · Category: D · Since: 2026-11-25
- Issue / Fix: Clear the view and return after redirecting.

### ZA-111 [Low/High] Hosted services and middleware ignore cancellation

- Where: `ZauberCMS.Core/Jobs/DailyJob.cs:22-52`, `ZauberCMS.Components/Seo/Startup/SitemapGeneratorService.cs:34,44,115`, `ZauberCMS.Core/Middleware/CultureMiddleware.cs:56` · Category: A · Since: 2026-11-25
- Issue / Fix: `BackgroundService` + `PeriodicTimer`; forward `RequestAborted`.

### ZA-112 [Low/High] SortableList drops callback tasks; `DotNetObjectReference` can leak

- Where: `ZauberCMS.Components/Shared/SortableList.razor:73-99` · Category: A · Since: 2026-11-25
- Issue / Fix: Return the task; try/finally on dispose.

### ZA-113 [Low/High] EF used directly in a context menu, scope held across a dialog

- Where: `ZauberCMS.Components/ContextMenus/ContentMenus/RestrictAccessContextMenu.cs:53-76` · Category: A/E · Since: 2026-11-25
- Issue / Fix: Move to a service method.

### ZA-114 [Low/High] Layouts set process-wide culture; sitemap formats with current culture and drops the home page

- Where: `ZauberCMS.Components/Admin/Layout/SectionLayout.razor:81-85`, `ZauberCMS.Components/Pages/BlankLayout.razor:29-33`, `ZauberCMS.Components/Seo/Startup/SitemapGeneratorService.cs:74-77,180-181` · Category: A/C · Since: 2026-11-25
- Issue / Fix: Remove `DefaultThreadCurrentCulture` writes; invariant culture; treat `Guid.Empty` redirect as null.

### ZA-115 [Low/High] Smaller performance items

- Where: `ZauberCMS.Core/Middleware/RedirectMiddleware.cs:16-17,88-92`, `ZauberCMS.Core/Extensions/StringExtensions.cs:300,374,390,405`, `ZauberCMS.Core/Data/Services/DataService.cs:140-150`, `ZauberCMS.Core/Content/Services/ContentService.cs:2157-2159,2261-2269` · Category: B · Since: 2026-11-25
- Issue / Fix: Redirect cache not invalidated on save (5 min, 5000 cap); regex built per call; paging without a stable order; `First` without `OrderBy`; a query per slug candidate; no cache stampede guard.

### ZA-116 [Low/High] Redirect rules containing "?" are treated as regex; no loop guard

- Where: `ZauberCMS.Core/Middleware/RedirectMiddleware.cs:140-150` · Category: C · Since: 2026-11-25
- Issue / Fix: Explicit "is regex" flag (schema).

### ZA-117 [Low/High] User `MapTo` overwrites identity fields from a stale editor copy

- Where: `ZauberCMS.Core/Membership/Mapping/UserMappingExtensions.cs:26-34`, `ZauberCMS.Core/Membership/Services/MembershipService.cs:133,147,197` · Category: C · Since: 2026-11-25
- Issue / Fix: A concurrent password reset or lockout is reverted; map profile fields only.

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

### ZA-127 [Low/Medium] Component lifecycle roll-up

- Where: `ZauberCMS.Components/Editors/GoogleMapsProperty.razor:57-65`, `ZauberCMS.Components/Editors/MultipleMediaUpload.razor:79-89` · Category: E · Since: 2026-11-25
- Issue / Fix: Work repeated on every parameter set; components assigning their own parameters; unkeyed loops (10 `@key` across 35 `@foreach`, no `Virtualize`).

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

### ZA-140 [Medium/High] Services do no authorisation of their own
- Where: every mutating method of `ZauberCMS.Core/*/Services/*Service.cs` (for example `ContentService.SaveContentAsync`, `MembershipService.SaveUserAsync`, `SeoService.SaveRedirectAsync`) · Category: D · Since: 2026-11-25 (split out of ZA-132 when its route half was fixed)
- Issue / Trigger: Access is decided by the page that calls a service. Any code that can resolve a service (a front-end component, a plugin, an interactive component on a public page) can save or delete anything; the services never check who is signed in.
- Fix (owner decision D10, 2026-11-25): an opt-in permission hook as its own phase (Roadmap Phase 10a). Services ask a replaceable permission service before a write; the default allows everything, a site can switch on a strict mode; front-end and plugin code that saves on purpose declares it. · Blocked by: design with the owner (Phase 10a); strict-by-default is a v5 question.

### ZA-144 [Low/High] A pending draft keeps the old URL after Restore gave the page a new one
- Where: `ZauberCMS.Core/Content/Services/ContentService.cs` (restore URL suffix in `SaveContentAsync`),
  `ContentMappingExtensions.MapDraftTo` (copies `Url`) · Category: C · Since: 2026-11-25 (Phase 5 audit)
- Issue / Trigger: Restore gives a page `offers-1` because `offers` was taken meanwhile, but its draft still holds
  `offers`; the editor shows the draft's URL, so publishing is refused ("already used") until the URL is changed.
- Fix: update the draft's URL when restore adds a suffix (or have the editor prefer the live URL when it changed
  after the draft was saved). · Blocked by: nothing.

### ZA-145 [Low/High] Move and copy load the subtree twice
- Where: `ZauberCMS.Core/Content/Services/ContentService.cs` (`SaveContentAsync` loop check then path rewrite;
  `CopyContentAsync` descendants then `Include(PropertyData)` reload) · Category: B · Since: 2026-11-25
- Issue / Trigger: Moving a page with a large subtree walks it twice (untracked, then tracked); copy loads full rows
  and then loads them again with property data.
- Fix: project IDs only for the loop check / reuse the tracked load; walk IDs only in copy. · Blocked by: nothing.

### ZA-146 [Low/Medium] Two publishes of one page at the same moment: the second version row is not written
- Where: `ZauberCMS.Core/Content/Services/ContentVersioningService.cs` (`CreateVersionAsync`: `maxVersion` read
  before the transaction; flag clear by `ExecuteUpdateAsync`) · Category: A/C · Since: 2026-11-25 (Phase 6 audit)
- Issue / Trigger: two saves of the same page commit at once (two editors, or a double click). Under READ COMMITTED
  the second clear doesn't see the first insert, so the second insert breaks the unique "current published" index;
  `ContentService` logs it and the content save still succeeds, but that version is missing. Version numbers can
  also repeat. (Before Phase 6 both rows were stored, both flagged.) The flag switch also runs as a direct update,
  so `IBeforeEntitySave`/`IAfterEntitySave<ContentVersion>` hooks don't see the old version's flag go off.
- Fix: lock the content row first (`UPDATE … SET DateUpdated = DateUpdated WHERE Id = @id`) and read `maxVersion`
  inside the transaction, or retry once on a unique violation. · Blocked by: nothing.

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

### ZA-152 [Low/High] The tests cannot see a scoping mistake in "run as the new administrator"
- Where: `tests/ZauberCMS.Core.Tests/Infrastructure/TestSite.cs` (`TestAuthenticationStateProvider`, `TestSignIn`), `ZauberCMS.Core/Installer/Services/InstallService.cs` (`RunAsAsync`) · Category: F · Since: 2026-11-26 (Phase 7 audit)
- Issue / Trigger: the test provider writes the signed-in user to one object shared by every scope, while the real
  provider is per scope. `The_starter_kit_runs_as_the_new_administrator` would still pass if the installer set
  the user on the wrong scope. The browser run with a starter kit is what covers it today.
- Fix: a per-scope sign-in holder in the test site, falling back to the shared one. · Blocked by: nothing.

### ZA-155 [Low/High] Resized copies of a deleted image stay in `_mediacache`
- Where: `ZauberCMS.Core/Media/Services/MediaService.cs` (`PurgeAsync`), ImageResize cache root (`ZauberSetup.cs`, `_mediacache`) · Category: D · Since: 2026-11-26 (Phase 8 audit)
- Issue / Trigger: a permanent media delete removes the original file, not the resized copies ImageResize made of
  it. Their names are hashes of path, options and file date, so they are hard to guess, but a "delete it for
  good" request (GDPR) is not fully met. Documented in `Docs/Services.md`.
- Fix: remove the cache entries for the file on purge (needs an ImageResize API for it) or prune the cache. ·
  Blocked by: the ImageResize package.

### ZA-156 [Low/High] The media recycle bin lists 500 items and does not open a binned folder
- Where: `ZauberCMS.Components/Admin/MediaSection/Navigation/MediaRecycleBinTree.razor` · Category: E · Since: 2026-11-26 (Phase 8 audit)
- Issue / Trigger: the tree shows the 500 most recently binned top-level items, and a binned folder cannot be
  expanded to see what went to the bin with it (restore or delete the folder as a whole). The content bin has the
  same limits (300).
- Fix: page the bin, and let a binned folder expand. · Blocked by: nothing (UX).

### ZA-157 [Low/High] Save hooks do not see the media below the item, nor an emptied bin
- Where: `ZauberCMS.Core/Media/Services/MediaService.cs` (`DeleteMediaAsync`, `RestoreMediaAsync`, `EmptyMediaRecycleBinAsync`) · Category: F · Since: 2026-11-26 (Phase 8 audit)
- Issue / Trigger: `IBeforeEntitySave<Media>` / `IAfterEntitySave<Media>` run for the item a call names, not for the
  items below it that are binned, restored or deleted with it, and not at all when the bin is emptied. A plugin
  that mirrors media elsewhere misses those. Content behaves the same. Documented in `Docs/Services.md`.
- Fix: run the hooks per item (a veto on one item would then have to stop the whole group). · Blocked by: design
  of the hooks for group changes.

### ZA-159 [Low/High] Content save hooks don't run per block on Clear changes and version restore
- Where: `ContentService.ClearUnpublishedContentAsync`, `ContentVersioningService.PublishVersionAsync` · Category: F · Since: 2026-11-26 (Phase 9 audit)
- Issue / Trigger: blocks deleted by Clear changes, and blocks rewritten or deleted by a version restore, don't run
  `IBeforeEntitySave<Content>`/`IAfterEntitySave<Content>`; in a "Save only", block drafts run none and a new block
  (a row only the draft reaches) fires "Added". A plugin mirroring content elsewhere (a search index) misses or
  indexes these. Same family as ZA-012 and ZA-157. Documented in `Docs/BlockListEditor.md` §4.
- Fix: pass those rows to the many-entity `SaveChangesAndLog` overload; decide what hooks a draft should see.


### ZA-160 [Low/Medium] A component on the media folder page calls JavaScript while its circuit is torn down

- Where: `ZauberCMS.Components/Admin/MediaSection/Folder.razor` page (component not identified) · Category: E · Since: 2026-11-26
- Issue / Trigger: A full reload of a media folder page logged `JSDisconnectedException` from `Renderer.Dispose` ("Unhandled exception rendering component", then "Unhandled exception in circuit") in the old circuit. Harmless for the user (the circuit is already gone), but it is an error in the log on every such reload. `MultipleMediaUpload` already catches it, so it is another component on the page (Radzen or ours).
- Fix: find the component whose `DisposeAsync` calls JS without catching `JSDisconnectedException`. · Blocked by: nothing.

### ZA-161 [Low/Medium] Choosing a tree's "Show more" row drops the highlight on the selected node

- Where: `ZauberCMS.Components/Trees/ContentTree.razor`, `ZauberCMS.Components/Trees/MediaTree.razor` (`OnChangeHandler`) · Category: E · Since: 2026-11-26
- Issue / Trigger: Radzen moves its selection to the row when it is clicked; the tree puts its `Value` back, but Radzen re-syncs the highlight only from a null value, so the open page is no longer highlighted (the selection itself is kept).
- Fix: render the row as a button inside the template with `@onclick:stopPropagation`, so the tree item is never selected. · Blocked by: nothing.

## Fixed this run / Resolved since last run

- **Phase 10 (2026-11-26), step D: circuits and contexts.**
  - **ZA-080 [Medium] AppState handlers run on other users' circuits without marshalling or filtering — fixed.** The version panel reloads only for its own page, on `InvokeAsync` (the editor and media page were fixed in Phases 8–9); every AppState handler in the admin now marshals. Browser: two saves with the panel open.
  - **ZA-086 [Medium] UserManager and its DbContext live for the whole circuit — fixed.** `ContentService` and `EmailService` resolve `UserManager` from a scope per call (the constructor parameter is gone); `UserEditor` uses a scope for the security-stamp update.
  - **ZA-011 [Medium] Two DbContext instances per scope — fixed.** `IZauberDbContext` resolves to the scope's `AddDbContext` instance, the one the Identity stores use; `UserEmailStore` no longer disposes the context DI gave it. The skipped regression test is un-skipped and also checks the user store's context. The scope-per-method pattern (vs `IDbContextFactory`) stays a known deviation in Standards.
- **Phase 10 (2026-11-26), step C: async paging and admin trees.**
  - **ZA-054 [High] Admin trees block the circuit with sync-over-async (latent deadlock) — fixed.** No tree waits on a task any more: content and media trees load each level with its child flags (`GetContentTreeLevelAsync`, `GetMediaTreeLevelAsync`) and `HasChildren` reads `TreeState`; content-type trees load the whole tree once and expand from memory; the folder picker in `MultipleMediaUpload` uses `MediaTree`; list editors call the new `IDataListSource.GetItemsAsync` (default method, `ContentTypesDataSource` implements it). Browser: content, media, structure and recycle-bin trees.
  - **ZA-068 [Medium] Admin trees over-fetch and silently stop at 100 children — fixed.** D26: tree levels load without property values or children, 100 at a time, with a "Show N more" row (content, media, recycle bin) or button (media folder grid). Tests `ZA068_…` (×2). Browser: 107 children under a page, 106 in a media folder, both paged; expanded nodes stay open after a refresh.
  - **ZA-082 [Medium] Sync EF calls inside async service methods (~30 sites) — fixed.** `ToPaginatedListAsync` everywhere (the sync paging and `BuildPath` are obsolete), `BuildPathAsync`, async URL uniqueness and role updates, and the remaining `FirstOrDefault`/`Any`/`ToList` calls in the content, language, tag, media and membership services. Test `ZA082_…` (in-memory paging); the whole suite runs on the async paths.
- **Phase 10 (2026-11-26), steps A and B: front-end caching, queries, version retention, bulk operations.**
  - **ZA-056 [High] Role queries always load every user in the role — fixed.** `GetRoleAsync`/`QueryRolesAsync` load members only with the new `IncludeUsers`. Test `ZA056_…`. Release note: code that read `Role.UserRoles` sets `IncludeUsers = true`.
  - **ZA-057 [High] Block lists and single media pickers are uncached on the front-end — fixed.** `GetBlocks` (no pending changes) and the single-media `GetMediaItems` path are cached under their prefixes; paging skips the `COUNT` when the first page isn't full. Test `ZA057_…` (paging counts).
  - **ZA-061 [Medium] Front-end entry cache is 5 seconds, not 5 minutes; key includes host and query string — fixed.** 5 minutes sliding; key on slug + scheme/host/path (no query string); domain save/delete clears the Content prefix; language keys and the default-language fallback applied outside the cache; a lookup without a full URL no longer throws. Tests `ZA061_…` (×4).
  - **ZA-062 [Medium] "Cached" content queries still hit the database — fixed.** `ContentTypeAlias` filters by a join instead of a lookup before the cache check; every service builds the SQL-hashed key only when `Cached`. Test `ZA062_…`.
  - **ZA-063 [Medium] Every extension component is instantiated per request and per block — fixed.** `EntryPage` uses the cached view types, `RenderBlock` a static alias → type map per view kind, `App.razor` reads editor scripts/styles once and only in the admin. Verified by build and the front-end tests; no browser run.
  - **ZA-064 [Medium] 404s cost two uncached queries each — fixed.** `AnyContentAsync` is cached under the Content prefix and ignores recycled content. Test `ZA064_…`.
  - **ZA-069 [Medium] Content versions grow without bound — fixed.** D25: `Zauber:ContentVersionsToKeep` (default 50, 0 = all) in the three appsettings; the daily job calls the new `IContentVersioningService.PruneVersionsAsync` (keeps the published version and latest draft, clears `ParentVersionId` links first). The version list is no-tracking, `CreatedBy` slimmed to id/name/email, `IncludeSnapshots = false` for the admin list. Tests `ZA069_…` (×3).
  - **ZA-071 [Medium] Language dictionary load is N+1 and synchronous — fixed.** Three async projections and a lookup. Test `ZA071_…`.
  - **ZA-073 [Medium] Bulk admin operations run item by item — fixed.** Public access goes through the new `IContentService.SetContentRolesAsync` (one transaction and audit row, `ContentRole` hooks, no versions; its "remove" path now clears the cache too); copy clones blocks one query per level; the sitemap pages through every page (up to 50,000), caches the SEO property per content type and writes invariant-culture priority and UTC `lastmod`. Tests `ZA073_…` (×2); copy covered by `ContentTreeSaveTests`; sitemap not run.
  - **ZA-083 [Medium] Daily audit cleanup loads every expired row, on every app start — fixed.** `ExecuteDeleteAsync`; the daily job's first run is five minutes after start-up. Test `ZA083_…`.
  - **ZA-084 [Medium] Non-image uploads are buffered twice in memory; raw exception text shown — fixed.** Streamed to a `FileStream` (a partial file is removed on failure); the error is logged and the editor gets a generic message. `DiskStorageProvider` takes an optional `ILogger`. Not covered by a test (needs an `IBrowserFile` upload); build only.
- **Phase 9 (2026-11-26): Block List redesign (D5).** How it works: [Docs/BlockListEditor.md](../Docs/BlockListEditor.md) (its §6 maps every old failure to the fix).
  - **ZA-034 [High] Cancel in a nested panel leaked its changes — fixed.** `ContentEditScope`: each block panel edits
    in a child scope; the list commits it on Save and rolls it back on Cancel or close, deeper panels included.
    Tests: `ContentEditScopeTests`, `ContentTreeSaveTests` (unreached drafts ignored). Browser: cancel at levels 2–4.
  - **ZA-035 [High] Root replaced pending changes (two block lists) — fixed.** One draft map per edit; the save walks
    every value. Test: two lists both saved.
  - **ZA-036 [High] Nested update dropped when pending as added — fixed.** The newest draft wins in the scope; the
    skipped test is replaced by `Editing_a_block_again_keeps_the_newest_values` and a tree-save test.
  - **ZA-038 [High] "Save only" saved blocks live — fixed.** `SaveContentAsync` with `SaveUnpublishedOnly` gives
    edited blocks drafts and inserts new ones only the draft reaches; publish promotes block drafts; Clear changes and
    version restore drop them. Tests at depth 1 and 2; browser at depth 4.
  - **ZA-040 [High] Hard delete notified `null` to every editor — fixed.** Delete and version restore name the item;
    the editor reloads only for its own item, on `InvokeAsync`, and warns when that drops unsaved block edits.
  - **ZA-008 [Medium] Version taken before the blocks were saved — fixed.** One version after the commit, with the
    blocks as saved (draft versions record block drafts). Test.
  - **ZA-009 [Medium] Block-list save not atomic — fixed.** `SaveContentAsync` with `Blocks`: one context and transaction;
    a failure on one block writes nothing. Test.
  - **ZA-010 [Medium] Block stylesheets leaked into the admin — fixed.** No global `<link>`; stylesheets go only into
    each preview's shadow root (`block-preview.js`).
  - **ZA-037 [Medium] Deleting a block kept its pending descendants — fixed.** The save deletes everything the page no
    longer reaches, live or draft. Test.
  - **ZA-039 [Medium] Stale editor state after save — fixed.** The editor reloads with a fresh scope; lists reload.
    Browser: two saves in a row without reload.
  - **ZA-041 [Medium] Re-entrant bubbling doubled per level — fixed.** No callbacks (`ISupportsBlockListChanges` and
    friends are obsolete and never called).
  - **ZA-042 [Medium] `OnParametersSetAsync` rebuilt the working copy — fixed.** `ContentEditor` loads once per
    (id, parent, content, scope), recorded before the load; a new id no longer reuses the previous item.
  - **ZA-043 [Medium] Blazor nodes moved into a shadow root — fixed.** `BlockPreview` renders the preview to static
    HTML with `HtmlRenderer` (one at a time per circuit, inert) and sets it into the shadow root from JS.
  - Also: `INestedContentReferenceProvider` (discovered) is how Core finds blocks for save, delete, snapshots,
    restore, copy and validation; copies of blocks now belong to the copy; the 20-level limit is exact.
  - `/audit changed` (two reviewers, plus a search of the git history for every old block-list problem at the
    owner's request) found, and this phase fixed before the commit: version restore deleting blocks that old-style
    versions have no snapshot of (High); a page stuck after a single-item publish left block drafts; two editors'
    "Save only" overwriting each other's drafts and deleting blocks (now refused); the stored draft link; previews
    racing, re-rendering on every reorder, rendering in parallel, and navigating away through links; an open
    panel's edit lost without a word when the page reloaded; editors reloading on unrelated events; Clear changes
    loading twice; block edit/delete not reachable by keyboard; custom editors' unsaved blocks missing in previews.
    Logged: ZA-159.
  - **ZA-158 [Low] Publishing a page from code left its blocks' drafts — fixed (owner decision).** `SaveContentAsync`
    of an item's values is the page-with-blocks save (`SaveContentParameters.Blocks`; the separate
    `SaveContentTreeAsync` was folded in before release), so it publishes block drafts and deletes unused blocks.
    Test: `Publishing_a_page_without_passing_blocks_publishes_its_block_drafts`.
- **Phase 8 (2026-11-26): media recycle bin (D3).** How it works: [Docs/Services.md](../Docs/Services.md) "Deletes".
  - **ZA-006 [High] Media recycle bin had no UI — fixed.** Media section → Recycle Bin (`MediaRecycleBinTree`) with
    Restore, Delete permanently and Empty recycle bin (`ContextMenus/MediaRecycleBinMenus/`). Deleting moves an item
    and everything below it to the bin (`DeleteMediaAsync`); `RestoreMediaAsync` brings back that group; a permanent
    delete removes the rows and the files, files first so that a file that can't be removed rolls the delete back.
    The confirmation lists the pages and members that still use the media (`GetMediaUsageAsync`). Recycled media is
    served to administrators only (404 for everyone else). Tests: `MediaRecycleBinTests` (17, on all three
    databases), `RestrictedMediaTests`. Browser: bin, 404 for anonymous and 200 for an administrator, restore,
    permanent delete with the usage warning, empty bin, files and their folders gone.
  - **ZA-013 [Medium] Media delete notified before the save — fixed.** `MediaService` raises media events once,
    after a successful save, for save, recycle, restore, purge and empty; `DeleteMediaContextMenu` no longer raises
    its own. Tests: the two event tests in `MediaRecycleBinTests`.
  - **ZA-154 [High] Restricted media could be fetched with a doubled slash — fixed** (found by this phase's audit;
    older than the phase). `RestrictedMediaMiddleware` compared the raw path, which Kestrel does not clean up, while
    the static file handler reads `/media//x.pdf` and `//media/x.pdf` as `/media/x.pdf`. Paths are now normalised
    (`NormaliseMediaPath` merges slashes) before both the folder check and the lookup, and a restricted or binned
    file that is let through is sent with `Cache-Control: private, no-store`. Tests: `RestrictedMediaTests`.
  - **ZA-153 [Low] The installer's SQL Server test said a just-created database was missing — fixed** (found by
    running the SQL Server tests for the first time). SqlClient's pool remembers a failed open for a few seconds,
    so after "database does not exist" every test with the same string failed the same way. The installer's test
    connections no longer use the pool (PostgreSQL's already didn't). Test: `DatabaseSetupOnServerTests` (SQL
    Server).

- **Phase 7 (2026-11-26): installer, first administrator, registration.** How it works: [Docs/Installer.md](../Docs/Installer.md).
  - **ZA-004 [High] First registered user became Admin — fixed.** `RoleExtensions.AssignStartingRoleAsync` no
    longer looks at how many users exist. The administrator is created by the installer (`InstallerMiddleware`,
    `IInstallService`), which is reachable only while the database has no installed flag (`GlobalData` alias
    `ZauberInstalled`). The flag is never removed, and a database with any user row, deleted or not, gets it at
    start-up, so the path cannot reopen by deleting accounts. Recovery for a site without an administrator is the
    configuration key `Zauber:Identity:RecoverAdminUser`. Accepted by the owner (D20): until a new site is
    installed, whoever reaches `/install` first becomes its administrator; both pre-install states log a warning.
    Tests: `InstallServiceTests`, `InstallerMiddlewareTests`, `DatabaseSetupTests`, `StartingRoleTests`,
    `RecoverAdminTests`. Browser: see the roadmap, Phase 7.
  - **ZA-023 [Medium] Admin-list promotion without proof of the address — fixed (D22, built differently).**
    Promotion at registration needs `EmailConfirmed`, which only a provider-verified external sign-up has. A
    password registration with a listed address gets the starting role and is emailed a link to choose the
    password; `MembershipService.ResetPasswordAsync` then confirms the address and promotes
    (`PromoteListedAdminAsync`). `ConfirmEmailAsync` deliberately does not promote: the first version did, and
    the phase's audit showed that a stranger who registers with an administrator's address and their own
    password becomes an administrator when the confirmation link is opened by its recipient or a mail scanner.
    The list is compared without case. Tests: `StartingRoleTests` (the attack, the reset path, the change-email
    path, the email that is sent). Not verified: delivery to a real mailbox.
  - **ZA-024 [Medium] Registration flooding — fixed (D23).** `GlobalSettings.AllowRegistration` (off: page 404,
    link hidden, external first sign-in refused); honeypot + minimum fill time (`IFormProtectionService`); own
    rate limit policy `"register"` (5 POSTs per address in 10 minutes); discovery interface `ICaptchaProvider`,
    no provider shipped. Tests: `FormProtectionTests`, `ExternalSignUpTests`. Browser: too fast, honeypot,
    success, 429 after five posts, 404 when off. Not verified: a real CAPTCHA provider.
  - **ZA-148 [Medium] The home page did not run in its content language — found and fixed in this phase.**
    Since the ZA-143 fix `CultureMiddleware.IsContentEndpoint` accepted only the `{**slug}` catch-all, but `/` is
    answered by `ZauberCMS/Components/Pages/Index.razor` (`@page "/"`), so the home page ran in the default
    request culture while every page below it ran in its own. The Phase 6 test used the catch-all endpoint for
    `/`, which a real request never has. Now a Razor page at `/` counts as content. Seen in the browser
    (`<html lang>` of `/` was `en`, of `/about` `de`; both `de` after the fix). Tests: `ContentCultureTests`
    (`The_home_page_*`).

  - **Found by `/audit changed` on this phase and fixed before the commit** (three independent reviewers):
    the ZA-023 promotion above; an install that a closed tab could leave half-done, with registration open
    against the installer's choice (`GlobalSettings` now go in before the account, no request cancellation from
    the account onwards, a half-made account is removed); the provider sticking after one wrong value typed by
    hand (`InstallState` fixes it only once a database has been prepared); "has users → installed" able to fire
    in the middle of an install (only start-up and the install itself apply that rule now); `ISeedData` able to
    run twice at once (`InitialiseDatabaseAsync` serialises); `AppSettingsWriter` dropping the file's
    permissions and breaking a file that uses flat `Zauber:Key` names; a starter kit whose constructor throws
    taking the installer down; no error page for the installer's GET; `RecoverAdminUser` matching a user name
    that looks like an email address and leaving an unconfirmed account unable to sign in;
    `IMembershipService.ExternalLoginAsync` creating accounts with registration off (the rest of ZA-105 stands);
    a SQLite path on a network share accepted by the database step; raw exception text on the installer's
    catch-all error page.
  - **Accepted, not fixed:** a live site that loses its database settings shows the installer (owner decision
    D24: documented and logged as an error at start-up); before install, the database step lets a visitor make
    the server try connections and check paths (D20: install before the site is public); the factory
    registrations of the DbContext and Identity stores use reflection on every resolve (microseconds, with ZA-011).
- **ZA-147 [Low] resolved — not a bug (checked in the browser, 2026-11-25).** Sample site on a scratch copy of the
  dev database, a temporary interactive probe on the German and Spanish pages, enhanced navigation between them and
  the English contact page. Order seen in the log: the circuit hears of the new address first, then the browser
  fetches the page, then the page's new components start — in a later activity, so the handler already sees the
  new address: a new component started in `de-DE` / `es-ES` as it should, also for a page this server had not
  served before. What looked like the bug: a component on both pages is kept by Blazor and not re-rendered, so it
  still shows what it rendered on the previous page; its next activity runs in the new page's language. Documented
  in `Docs/FrontEnd.md`; regression test for an address change between activities.
- **Roadmap Phase 6 — PostgreSQL & Redis made real — fixed 2026-11-25.** Build 0 warnings; 428 tests (425 pass, 3
  skipped for other open findings), including the content, data-grid, media, user-delete and provider-query tests on
  a real PostgreSQL and the Redis tests against a real Redis (Testcontainers). `/audit changed` (two independent
  reviewers) found 14 more problems in the change: 12 fixed (listed under the findings below), two logged as
  ZA-146 and ZA-147 (ZA-147 later resolved as not a bug). App run on PostgreSQL + Redis containers: migrations, registration, admin, a settings save
  published through Redis; no errors logged.
  - **ZA-050 [High] PostgreSQL install failed; unique version indexes missing.** Reproduced first: `Initial` failed
    with `42601: syntax error at or near "["`. Index filters are now per provider (`ZauberDbContextBase`), the three
    `ContentId` indexes are named, the PostgreSQL `Initial` was corrected (D14) and migration
    `ContentVersionIndexes` adds the unique "current published" index after keeping one flagged version per page
    (the one published last, then the highest number — review fix). Version flags are switched in the database
    inside the save's transaction (a one-save switch hit the new index intermittently). Tests: migrate on
    PostgreSQL, the index refuses a second flag, the upgrade keeps the right version, flags move on new versions.
  - **ZA-007 [High] Raw SQL failed on PostgreSQL.** `WherePathLike`/`WhereHasCompositionsUsing` take table and
    column names from the model, quoted by the provider. Tests fail on PostgreSQL against the old code.
  - **ZA-017 [Medium] Search under a Turkish server culture.** Search terms are lowered with `ToLowerInvariant`; the
    review found the same problem in slug generation, the culture/redirect middleware, the content finder, the disk
    storage extension check and an SEO check, all changed too. Tests: search ×3 services, slug.
  - **ZA-122 [Low] Design-time factory file name** → `appsettings.json`.
  - **ZA-051 [High], ZA-014 [Medium], ZA-055 [High], ZA-072 [Medium] cache redesign (D13).** Values stay in each
    server's memory (`ServerCache`); Redis carries only invalidations (`RedisCacheInvalidationBus`: counter + pub/sub,
    full clear on a gap, when behind on two checks in a row, on a counter reset, on reconnect; unsent clears become a
    full clear). The key index is per server, guarded per key, and an eviction removes only its own entry; a value
    loaded while a clear ran is not kept; query keys include `Include` calls and the values captured in filtered
    includes. Content saves, recycle, purge and content-type delete clear again after commit. Tests: restricted page
    keeps its roles from the cache, nothing of a user in Redis, clear reaches the other server, missed message,
    behind on two checks, counter reset, the replaced-entry race and the overlapping clear (both fail on the old
    service), include and filtered-include keys.
  - **ZA-143 [Low] Non-page requests looked up as the home page.** `CultureMiddleware` resolves content only for the
    catch-all endpoint (`IsContentEndpoint`); `ContentCultureCircuitHandler` gives a front-end circuit its page's
    language (also under a path base). Tests: endpoint rule, middleware, circuit handler.
- **Roadmap Phase 5 — content integrity — fixed 2026-11-25.** Build 0 warnings; 315 tests (312 pass, 3 skipped for
  other open findings). `/audit changed` (two independent reviewers) found six more problems in the change; all
  fixed below except ZA-144 and ZA-145 (Low, open). Tests: `ContentIntegrityTests` (25 cases, all seen failing against the previous
  `ContentService`/`ContentVersioningService`), `ContentUrlTests`, `ContentLinkTests` (Components). "Browser" = the
  dev site on a scratch copy of the dev database.
  - **ZA-046 [High] Duplicate URL broke every front-end page.** The language lookup uses `TryAdd`. URLs are kept
    unique among live pages: a typed URL that another page has is refused, a generated, copied or restored one
    gets a `-1`, `-2`… suffix (restore says so in a warning). Copy no longer appends a fixed `-copy`.
  - **ZA-047 [High] Non-draft saves discarded the draft.** A save with `ExcludePropertyData` (sort, move, restrict
    access, culture & domains, restore) keeps the draft; a save with property data still publishes over it. The
    editor shows a draft with the new `ContentMappingExtensions.MapDraftTo`, so tree position, language and dates
    come from the live row and a later publish does not put a moved page back. A save without property data keeps
    the stored `UnpublishedContentId` even when the caller's copy predates the draft.
  - **ZA-048 [High] Recycling a parent left its children live.** Recycle marks every page below; Restore brings
    them back; permanent delete of a recycled page deletes the recycled pages below it (refused while one is live).
    Recycle stamps the group with one `DateUpdated` and Restore brings back only that group (a page binned on its
    own earlier stays binned); restoring a page under a parent still in the bin is refused.
    The recycle-bin tree and Empty Bin list only the top-most recycled items. Browser: About + two children →
    all 404, only About in the bin, Restore → all 200 again.
  - **ZA-049 [High] Copy shared the draft row.** Copies and cloned blocks get no `UnpublishedContentId`. Copy with
    descendants walks `ParentId` (no raw SQL), skips recycled pages and builds fresh paths.
  - **ZA-065 [Medium] Unpublished/deleted content through side paths.** Domain root and internal-redirect targets
    must be published, not recycled, not a block; included `Children` (front-end lookup, `QueryContentAsync`, the
    content grid) never hold recycled items, and on the front end only published ones.
  - **ZA-066 [Medium] Slug matching depended on the provider.** `GetContentFromRequestAsync` normalises the slug with
    the new `ContentUrl.Normalize` (lower case invariant, slashes trimmed; empty = root); saves store URLs the same
    way. Browser: `/DEUTSCH/` → 200.
  - **ZA-067 [Medium] Blocks had public URLs.** Blocks get no URL (also cleared on their next save; path URLs skip
    them); the lookup, the language map and the uniqueness check ignore nested rows, so old block URLs are inert.
  - **ZA-070 [Medium] Content-type delete checks.** The in-use check counts drafts and recycled pages; all nested
    rows are checked, not ten (stopping at the first live reference); orphan clean-up, audit row and delete
    commit in one transaction.
  - **ZA-074 [Medium] Draft versions held live values.** A draft version records the stored draft (name, navigation
    settings, values, blocks). `CreateVersionAsync` no longer writes the version back onto the row (that only
    changed `DateUpdated`, the concurrency token, so saving the item a publish returned was refused).
  - **ZA-075 [Medium] Version restore rewrote tree position and publish state.** Restore brings back name,
    navigation settings, values and blocks only; discards the pending draft; marks the version current only when
    the page is published; saves through `SaveChangesAndLog` in a transaction with an audit row.
  - **ZA-079 [Medium] Move updated only the moved row's path.** A save that changes the path rewrites every path
    below it in the same transaction, and a move under the page's own descendant is refused. Not done: with
    `EnablePathUrls`, moved pages keep their URL (URLs are only built on create), as before.
  - **ZA-016 [Medium] `IsDeleted = null`.** Kept as the documented "both" value (public API); included children no
    longer leak recycled items whatever it is set to. Tests pin all three values.
  - **ZA-135 [Medium] Rich-text content links rewritten to relative URLs.** The validator compares through
    `ContentUrl.UpdatedHref` (same form on both sides, keeps `?query`/`#fragment`, writes `/blog`) and no longer
    removes links to unpublished or recycled pages (only to missing ones). `SiteRelative` keeps anything that is
    not an http/https address on this site (`javascript:…`, `\host`), and the inserted href is attribute-encoded.
    Editing a link on the Manual tab keeps `data-contentid`
    (`InsertContentLinkItem.FindContentId` reads it from the editor HTML). Browser: `/blog` and `/blog#latest`
    unchanged on open; title added on the Manual tab, `data-contentid` kept.
  - Not verified in a browser: draft kept across a sort and then published (service tests only), version restore
    from the Versions tab, content-type delete messages, copy from the menu.

- **Phase 4 follow-ups (owner request) — fixed 2026-11-25.** Build 0 warnings; 259 tests (256 pass, 3 skipped).
  - **ZA-134 [Medium] Rich text lost iframes.** ZauberCMS.RTE 4.0.0: an allowed provider's iframe becomes an
    embed placeholder on load, and the new `embed` toolbar item inserts one. `IRichTextEmbedService`
    (`DefaultRichTextEmbedService`: YouTube, Vimeo; replaceable) gives the editor its provider list and expands
    placeholders at render time; `DefaultHtmlSanitizerService` calls it after sanitising. Iframes from other
    hosts are still removed by the editor (warning toast). Tests: `RichTextEmbedTests`. Browser: existing
    iframe → card → saved as placeholder → player on the public page; new embed through the button.
  - **ZA-136 [Low] RTE 3.0.0 workarounds.** Removed on 4.0.0: `GetLinkAtCursorAsync` no longer throws without
    a link, `LinkInfo.Rel` is back (pre-filled in the dialog). Selecting the link before the dialog opens
    stays, as the normal way to do it. Browser: create, re-edit, no nested link. The `data-contentid` point
    moved to ZA-135.
  - **ZA-141 [Medium] Admin ran in the language of the root content.** With a language set on the root content,
    relative dates (Humanizer) and date/number formats in the admin were in that language: `CultureMiddleware`
    resolved the `/_blazor` connection request as the home page and the circuit kept that culture.
    `SectionLayout` set the culture only for its first render (and set the process-wide default culture from
    a request). Now `AdminCultureCircuitHandler` applies `Zauber:AdminDefaultLanguage` to every activity of a
    circuit whose address is under `/admin`, and the middleware does the same for `/admin` requests. Tests:
    `RedirectAndCultureTests`. Browser: dashboard dates in English with a Spanish root (Spanish before).
  - **ZA-142 [Low] A new redirect did nothing for up to five minutes.** `RedirectMiddleware` keeps a compiled
    list in `IMemoryCache` for five minutes and nothing cleared it; a deleted redirect also kept working.
    `SeoService` now drops the list on save and delete (per process). Seen in the browser as a 404 on a
    just-created redirect; test in `RedirectAndCultureTests`.
  - Redirect editor (no ID; owner decision D11): a full http/https target is kept as typed
    (`RedirectTarget.Normalize`), an invalid one shows a message. Tests in `RichTextEmbedTests`.

- **Roadmap Phase 4 — access control & identity — fixed 2026-11-25.** Build 0 warnings; 231 tests (228 pass, 3
  skipped for other open findings). Owner decisions D6–D9 in the roadmap. "Browser" below means the dev site on a
  scratch copy of the dev database.
  - **ZA-053 [High] Restricted media public when recycled / by URL case.** The lookup keeps recycled media and
    compares normalised paths (lower case, forward slashes, no leading slash). Tests: `RestrictedMediaTests`
    (3 of 6 seen failing before the fix).
  - **ZA-052 [High] `refreshsignin` renewed any cookie.** The endpoint needs a signed-in user who may still sign in
    (not deleted, not locked out) and either a cookie whose security stamp is still valid or the two-minute ticket
    a save hands out when you change your own account (`RefreshSignInTicketService`,
    `HandlerResult.RefreshSignInTicket`). Anything else signs out. Cookies are re-checked against the database
    every 5 minutes (was 30). Tests: `SignInHardeningTests`. Browser: rename own user → still signed in, back on
    the user page; anonymous call → 401. Not verified: a browser run with a cookie whose stamp was invalidated by
    another session (covered by the ticket tests only).
  - **ZA-059 [Medium] Lockout never triggered.** Both password sign-ins pass `lockoutOnFailure: true`. Tests:
    six wrong passwords lock the account and the right one is then refused with a message.
  - **ZA-002 [High] External sign-up always refused.** Google now maps `email_verified` from its user-info
    response. Facebook and Microsoft do not state it, so they create accounts only when the new
    `Zauber:Identity:ExternalProviders:<Provider>:TrustEmail` is true (default false). The account, login and
    starting role are created before the single sign-in; a half-created account is removed. Tests:
    `ExternalSignUpTests` (claims level). **Not verified live** against any provider (owner decision D8).
  - **ZA-020 [High] Grid filter/sort strings.** Corrected finding: the strings are parsed by Radzen's own parser
    (which already allow-lists methods), not by System.Linq.Dynamic.Core. What was open: any column could be
    filtered or sorted on, including `PasswordHash` and `SecurityStamp`. `GridQueryGuard` now checks the parsed
    expression (entity properties and simple value methods only, secret columns never) in `DataService`,
    `LanguageService` and `ContentService`. Tests: `DataGridFilterTests` (10 refusals seen failing first).
  - **ZA-139 [Medium, new] Content grids refused every filter.** Found in the browser: an older text check in
    `ContentService.GetDataGridContentAsync` rejected any string containing `=>`, which is every filter the grid
    builds. Replaced by `GridQueryGuard`. Test added; browser: filtering "abo" on the content dashboard lists
    About.
  - **ZA-019 [High] SEO checker SSRF.** The checks use a named HTTP client (`SeoHttp`) that resolves the host and
    refuses non-public addresses at connection time (so redirects and DNS tricks are covered), with timeouts and a
    redirect limit. Exceptions: the host of `Zauber:SiteUrl`, and this application's own ports on loopback.
    `SeoChecker` no longer blocks the circuit and shows a message. Tests: `OutboundRequestTests`. Browser:
    `127.0.0.1` and `169.254.169.254` refused, `file://` refused, the site's own page checked.
  - **ZA-003 [High] Host-header email links.** New `Zauber:SiteUrl`; `ToAbsoluteUrl` uses it when set. When it is
    empty the old behaviour stays and a warning is logged at startup outside Development if `AllowedHosts` is `*`.
    Tests: `OutboundRequestTests`. Not changed: `AllowedHosts` stays `*` in the shipped appsettings (a default
    cannot know the host).
  - **ZA-060 [Medium] Proxy and transport hardening.** New `Zauber:ForwardedHeaders` (off by default; known
    proxies/networks). The admin IP allow-list moved from `SectionLayout` to `AdminAccessMiddleware` (403 for
    every `/admin` request from another address; compares addresses, not text). Upload-folder responses get
    `X-Content-Type-Options: nosniff`. Corrected finding: HSTS is already in the host `Program.cs` (dev site and
    template). Data-protection key persistence stays a hosting task (documented). Tests: `AdminAccessTests`;
    headers checked with curl. Not verified: anything behind a real proxy.
  - **ZA-132 [Medium] Admin authorisation fail-closed.** A page under `/admin` with no `[Authorize]` or
    `[AllowAnonymous]` of its own is treated as admin-only, by `AdminAccessMiddleware` for requests and by
    `Routes.razor` for navigation inside a circuit. Redirect targets are validated on save and when followed
    (`RedirectTarget`: a local path or an http/https address). Tests: `AdminAccessTests`. Browser/curl: anonymous
    `/admin`, `/ADMIN/seo/redirects` and `/admin/does-not-exist` all redirect to sign-in. The service half is
    the new open finding ZA-140.
  - **ZA-021 [Medium] Sanitiser.** `<iframe>` only from https hosts in `Zauber:HtmlSanitizer:AllowedIframeHosts`
    (default YouTube, Vimeo); `data:` only as a bitmap in `<img>`. The dev site's `RTEBlock` now sanitises.
    Tests: `UploadAndMarkupTests`.
  - **ZA-022 [Medium] Upload content check.** `UploadContentCheck`: signatures with offsets (MP4 with any box
    size, WebP/WAV told apart, office and archive formats) and a text check for text types (no binary, no HTML or
    script in `.txt`/`.csv`/…). Tests: `UploadAndMarkupTests`, including through `DiskStorageProvider`.
  - **ZA-005 [High] SVG.** Still off by default. When a site allows `.svg`, the upload is stored sanitised
    (`SvgSanitizer`: drawing elements only; no script, handlers, links or outside references) and every SVG from
    the upload folder is served with a sandboxing Content-Security-Policy. Tests: `UploadAndMarkupTests`
    (including an upload through `DiskStorageProvider`); header checked with curl. Not verified: an SVG upload
    through the admin UI. SVG files uploaded before this change are not rewritten (the header still applies).
  - **ZA-138 [Low] Duplicate `<RadzenComponents>`.** One host left. Browser: one notification on save, one
    context menu, one confirm dialog (Content); Media, Structure, Settings, Users and SEO pages load and their
    grids sort without errors. Not checked one by one: tooltips, and dialogs in sections other than Content.

- **Roadmap Phase 3 — broken admin actions — fixed 2026-11-25.** Build 0 warnings; 65 tests (62 pass, 3 skipped
  for other open findings). Each UI fix below was checked in the browser on a scratch copy of the dev database.
  - **ZA-001 [High] Recycle-bin Restore, Delete and open did nothing.** `RestoreContextMenu`,
    `FinalDeleteContentContextMenu`, `EditContent` and `ContentEditor.RefreshContent` now pass
    `IncludeDeleted = true`. Browser: recycle a page → open it from the bin (editor loads) → Restore (back in the
    content tree) → recycle again → Delete (gone). The service half was already covered by `ContentSoftDeleteTests`.
  - **ZA-045 [High] + ZA-088 [Medium] Delete user.** `DeleteUserParameters.Id` is the property; `UserId` is
    `[Obsolete]` and still honoured when `Id` is empty. The service writes once through `UserManager`
    (`UpdateSecurityStampAsync` stores the flag and the new stamp) and returns the Identity errors on failure; the
    unused second DbContext is gone. `UserEditor` had the success check inverted; it now shows "User deleted" or the
    errors. Tests: `DeleteUserTests` (the waiting skip is removed). Browser: delete a user → notification, back to
    the list, `Deleted = 1` in the database.
  - **ZA-137 [Medium, new] Dashboard grids listed soft-deleted rows.** Found while checking ZA-045: the deleted user
    stayed in "Latest Users". `DataService.GetDataGridAsync` had no soft-delete filter; it now leaves out deleted
    Content, Media and User rows unless the new `DataGridParameters.IncludeDeleted` is set. Tests:
    `DataGridSoftDeleteTests` (seen failing first). Browser: the deleted user is no longer listed.
  - **ZA-076 [Medium] Culture & Domains false "modified by another user".** Only the dialog saves now (with
    `ExcludePropertyData`, it only changes the language); the context menu no longer saves the same object a second
    time. The dialog edits a fresh copy instead of the tree's object, stays open when a save fails, and the menu
    reports an item that cannot be loaded. Tests: `ContentSaveTests` pins the rule behind the bug (an outdated copy
    is refused) and the language-only save. Browser: set a language and save, twice in a row → "Culture Updated"
    both times, no error, the saved language is preselected the second time.
  - **ZA-103 [Medium] Email confirmation always failed.** `ConfirmEmailAsync` decodes the Base64Url code; a malformed
    code gives a message instead of an exception. The change-of-email branch works: the emailed link now carries the
    new address (`&email=`), `ConfirmEmailParameters.NewEmailAddress` is new, and the page passes it on. The page
    shows a success message (it showed nothing before). `returnUrl` in the link is now URL-encoded. Tests:
    `ConfirmEmailTests` (confirm, change email, code refused for another address, malformed codes); the first two
    were seen failing without the fix. Not verified: a real email round trip (no email provider in the test site).
    ZA-023 (`SignInRequireConfirmedAccount`) stays open — it is a separate decision.
  - **ZA-058 [High] Layout re-render reloaded editors.** `SectionLayout`'s cascading value is `IsFixed`, and
    `ContentTypeEditor`, `UserEditor` and `RoleEditor` reload only when their route values change. Browser: edit a
    content type name, toggle the sidebar twice → the edit is still there; clicking another content type loads it,
    going back reloads the original. The data loss itself was never reproduced before the fix (finding was "read,
    not reproduced"), so this is verified as "does not happen now", not as a before/after.
  - **ZA-081 [Medium] Context-menu failures were silent.** `BaseTree` and `SectionTrees` catch a failing menu
    action, log it with the menu's type name and show a generic "Action failed" notification. Not verified by a
    forced failure in the browser; the normal path was exercised by every menu action above.

- **ZA-133 [Medium] Blazored.Modal is deprecated and archived — fixed 2026-11-25 (owner decision: vendor it).**
  Radzen side dialogs cannot nest (one slot; `OpenSide*` closes the open one — Radzen 13.3 `DialogService.cs`), and
  the admin needs nested right-hand panels, so the modal is kept. Its v7.3.1 source (MIT) now lives in
  `ZauberCMS.Core/Modal/` with unchanged namespaces; the NuGet reference is removed from all three csprojs and
  `ZauberCMS.Core` became a Razor SDK project to compile it. Assets moved to `_content/ZauberCMS.Core/`.
  Verified: build 0 warnings; in the browser a block-list panel, a nested panel on top of it (with the rich-text
  editor), closing the top one reveals the one beneath, closing again returns to the page; no server errors.
  Not verified: template scaffold / a consumer site built from the new packages. **Upgrade note for the release:**
  sites or plugins that reference the `Blazored.Modal` package themselves must remove it (type clash), and
  compiled plugins must be rebuilt (the types moved assembly).
- **ZA-102 [High] Four vulnerable NuGet packages — fixed 2026-11-25 (Roadmap Phase 1).** `dotnet list package
  --vulnerable --include-transitive` now reports none for every project; the 70 NU1902/NU1903 build warnings are gone.
- **ZA-130 [Medium] Dependencies behind — fixed 2026-11-25 (Roadmap Phase 1)**, except the two items below.
  Microsoft.* 10.0.7 → 10.0.15, Npgsql EF 10.0.4, **Radzen.Blazor 10.3.2 → 13.3.0**, MailKit 5.0.0, HtmlSanitizer
  11.0.26, **ZauberCMS.RTE 3.0.0**, HtmlAgilityPack 1.13.1, FluentValidation 12.2.0, DynamicLinq 10.7.4, BlazorMonaco
  3.5.0, TinyMCE.Blazor 2.2.2. No 11.x Microsoft/EF package is resolved. Code changes: `SmtpEmailProvider`
  (`SendRequest`), one nullable fix for HtmlAgilityPack, and the RTE 3 migration (`App.razor` script/CSS removals,
  `ZauberSetup` registration, both toolbar items, `ContentLinkDialog`, `ZauberCMSEditorProperty`, settings text).
  Verified: Release build 0 warnings / 0 errors; app run on SQLite against a scratch copy of the sample database
  with a real browser — register/sign in, content tree, block-list side panel with the editor, insert a content
  link, edit an existing link, save & publish and see the link on the public page, every admin section (media,
  structure, settings pages, users, SEO pages) rendered with no error UI and no server errors, public pages 200.
  **Not verified:** inserting a media item (the sample database has no media — the picker opened empty), two
  editors on one page, the user editor, large content, sending email through MailKit 5, the server-side sanitiser
  output on HtmlSanitizer 11, SQL Server / PostgreSQL / Redis, the template scaffold.
  Left open on purpose: Serilog.AspNetCore stays on 10.0.0 (11.0.0 pulls Microsoft.Extensions 11.x — Phase 12);
  Blazored.Modal → ZA-133. New findings from this work: ZA-133…ZA-136.
- **ZA-044 [Critical] SEO admin pages have no authorisation — fixed 2026-11-25 (Roadmap Phase 0).** Added
  `[Authorize(Roles = Constants.Roles.AdminRoleName)]` to `Seo/SeoRedirects.razor`, `Seo/SeoSitemaps.razor` and
  `Seo/PageChecker.razor` (per page, not a `Seo/_Imports.razor`, so the public `RenderSeoTags` component is
  untouched). They were the only routable components outside `Components/Admin/`. Verified: Release build 0 errors,
  70 warnings (unchanged); app run on SQLite, anonymous GET of all three URLs → 302 to `/Account/Login`. Not
  verified: the "before" state at runtime, a signed-in admin still reaching the pages, a signed-in non-admin being
  refused. The fix is on `rework-2026` only — **published 4.1.3 packages still have the hole** until a release.
  Follow-up hardening split out as ZA-132.
- Nothing fixed (report mode) and nothing resolved: no code changed between the seed report (`3eb5cb7`) and this run.
- All 43 seeded findings re-verified as still open. Severity raised on ZA-002 (confidence), ZA-019 and ZA-020 (now
  reachable without login). Citations corrected on ZA-013, ZA-034, ZA-035, ZA-036, ZA-041.
- Docs corrected: soft-delete parameter naming (`Docs/Services.md`), pre-created tag behaviour (`Docs/Releasing.md`),
  bootstrap discovery visibility and the lockout claim (`AGENTS.md`), `TreeButton` location (`Docs/AdminUI.md`).

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

## Coverage

- Reviewed: all six categories (A async/lifetimes, B data/performance, C correctness, D security, E Blazor/render
  modes, F architecture/packaging) across Core, Components, Routing, ZauberCMS, the template and CI.
- Front-end hot path measured by reading: about 10 DB round-trips cold; 0 within 5 s of the same URL; 4-5 after 5 s
  idle; 2 per 404; plus 3 per block list and 1 per single media picker on every request.
- Not covered: any runtime verification (browser, PostgreSQL, Redis, proxy); `app.db` contents; internals of the
  ImageResize, ZauberCMS.RTE, TinyMCE and Monaco packages; `wwwroot` JavaScript beyond a sink grep; full bodies of
  `SeoService`, `TagService`, `LanguageService`; per-editor settings round-trip (only Numeric and ContentPicker read);
  `ContentTypeEditor`, compositions, and the Sort/Move dialogs; double-submit and re-entrancy in admin dialogs;
  focus and keyboard behaviour in side panels; Radzen 10 obsolete API usage (hidden by `NoWarn`).
- Runtime checks done in a browser so far (scratch SQLite database): the Phase 1 package/editor smoke test and the
  Phase 3 admin actions and the Phase 4 checks listed under "Fixed". Phase 6: the app on PostgreSQL + Redis
  containers (see Phase 6 under "Fixed"); the test suite runs on PostgreSQL and Redis, and on SQL Server when
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
