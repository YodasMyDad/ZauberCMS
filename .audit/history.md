# ZauberCMS Audit History

Append-only record of what each audit run and Roadmap phase fixed, resolved, closed or declined, newest first,
with how it was verified. Open items are in [report.md](report.md); IDs are never reused. Until 2026-11-27 this
was the "Fixed" section of `report.md`, so older Roadmap notes that say "Details: audit report" mean this file.

Each run adds one section at the top of "Fixed, resolved, closed and declined" (see the audit skill, §9), and
one line at the top of the run log.

## Run log

After Roadmap Phase 10f-1 (2026-11-27): Build PASS · Warnings: 0 · Vulnerable packages: 0 · Tests: 1205 (all pass, none skipped), SQL Server (LocalDB), PostgreSQL and Redis · Template smoke test passes; `doublecheck` self-review on the part. ZA-155, ZA-163, ZA-193 fixed (D62, D63). Next free ID: ZA-199
After the Phase 11 audit follow-ups (2026-11-27): Build PASS · Warnings: 0 · Vulnerable packages: 0 · Tests: 1175 (all pass, none skipped), SQL Server (LocalDB), PostgreSQL and Redis · `doublecheck` self-review. ZA-185 (behaviour, D60) and ZA-198 (D61) fixed. Next free ID: ZA-199
After Roadmap Phase 11 (audit, 2026-11-27): Build PASS · Warnings: 0 · Vulnerable packages: 0 · Tests: 1165 (all pass, none skipped), SQL Server (LocalDB), PostgreSQL and Redis · `/audit changed` over 2f4dc41..773505c (deterministic checks; unit reviewers for the front-end path, bootstrap and the public surface; two independent diff reviewers given only the code; two verifiers). Nothing Critical or High from Phase 11. ZA-169…ZA-186 found and fixed (7 Medium, 11 Low); ZA-187…ZA-198 recorded (1 Medium, 11 Low, all older than Phase 11) and assigned to 10f, 10g, 10i and 12; owner questions ZA-185, ZA-198
After Roadmap Phase 11g (2026-11-27): Build PASS · Warnings: 0 · Vulnerable packages: 0 · Tests: 1122 (all pass, none skipped), SQL Server (LocalDB), PostgreSQL and Redis · Template smoke test passes; `doublecheck` self-review on the part. ZA-125, ZA-126 fixed; content finders added at the owner's request (D55). Phase 11 done. Next free ID: ZA-169
After Roadmap Phase 11f (2026-11-27): Build PASS · Warnings: 0 · Vulnerable packages: 0 · Tests: 1109 (all pass, none skipped), SQL Server (LocalDB), PostgreSQL and Redis · Template smoke test passes; `doublecheck` self-review on the part. ZA-092, ZA-124 fixed. Next free ID: ZA-169
After Roadmap Phase 11e (2026-11-27): Build PASS · Warnings: 0 · Vulnerable packages: 0 · Tests: 1071 (all pass, none skipped), SQL Server (LocalDB), PostgreSQL and Redis · Template smoke test passes; `doublecheck` self-review on the part. ZA-090, ZA-091 fixed. Next free ID: ZA-169
After Roadmap Phase 11d (2026-11-27): Build PASS · Warnings: 0 · Vulnerable packages: 0 · Tests: 1050 (all pass, none skipped), SQL Server (LocalDB), PostgreSQL and Redis; `doublecheck` self-review on the part. ZA-120, ZA-121, ZA-167 fixed; the failing CI test of 11c (a Windows path separator) fixed. Next free ID: ZA-169
After Roadmap Phase 11c (2026-11-27): Build PASS · Warnings: 0 · Vulnerable packages: 0 · Tests: 1034 (all pass, none skipped), SQL Server (LocalDB), PostgreSQL and Redis · Template smoke test passes (Windows PowerShell 5.1; PowerShell 7 in the Linux SDK container), now a CI step; `doublecheck` self-review on the part. ZA-087, ZA-094, ZA-118, ZA-119, ZA-123 fixed; ZA-168 found and fixed; ZA-167 new (Phase 11d). Next free ID: ZA-169
After Roadmap Phase 11b (2026-11-27): Build PASS · Warnings: 0 (now errors; analysers D43) · Vulnerable packages: 0 (CI scans) · Tests: 1026 (1025 pass, 1 skipped for open finding ZA-118), SQL Server (LocalDB), PostgreSQL and Redis; `doublecheck` self-review on the part; CI not run (GitHub only; checked with actionlint/shellcheck and a scratch-repository run of its steps). ZA-089, ZA-109, ZA-131 fixed. Next free ID: ZA-167
After Roadmap Phase 11a (2026-11-27): Build PASS · Warnings: 0 · Vulnerable packages: 0 · Tests: 1016 (1015 pass, 1 skipped for open finding ZA-118), SQL Server (LocalDB), PostgreSQL and Redis; `doublecheck` self-review on the part. ZA-164, ZA-165 fixed; ZA-166 found and fixed. Next free ID: ZA-167
After Roadmap Phase 10e (2026-11-27): Build PASS · Warnings: 0 · Vulnerable packages: 0 · Tests: 993 (992 pass, 1 skipped for open finding ZA-118), SQL Server (LocalDB), PostgreSQL and Redis; `/audit changed` run on the phase (two reviewers: their three Highs — password guessing on unconfirmed accounts, the single-load cache scoped per request, the 404 that counted as the error page — their Mediums and Lows fixed; slow regex redirects, per-server throttles and users deleted before v5 noted). ZA-030, ZA-031, ZA-032, ZA-033, ZA-095, ZA-098, ZA-104, ZA-105, ZA-106, ZA-107, ZA-110, ZA-111, ZA-114, ZA-115, ZA-116, ZA-149, ZA-150 fixed; ZA-164 and ZA-165 new (Phase 11). Next free ID: ZA-166
After Roadmap Phase 10d (2026-11-27): Build PASS · Warnings: 0 · Vulnerable packages: 0 · Tests: 912 (911 pass, 1 skipped for open finding ZA-118), SQL Server (LocalDB), PostgreSQL and Redis; `/audit changed` run on the phase (two reviewers, nothing High: the Medium — Radzen dialogs outside every error boundary — and the Lows fixed; the bins reloading on any save noted). ZA-025, ZA-085, ZA-096, ZA-097, ZA-101, ZA-108, ZA-156, ZA-161 fixed; ZA-026 and ZA-160 resolved. Next free ID: ZA-164
After Roadmap Phase 10c (2026-11-26): Build PASS · Warnings: 0 · Tests: 897 (896 pass, 1 skipped for open finding ZA-118), SQL Server (LocalDB), PostgreSQL and Redis; `/audit changed` run on the phase (one reviewer, nothing High; its two Mediums — a second pick during staging could end the circuit, a new validator instance per render cleared the errors — and its Lows fixed). ZA-015, ZA-027, ZA-028, ZA-029, ZA-078, ZA-100, ZA-112, ZA-127 fixed. Next free ID: ZA-164
After Roadmap Phase 10b (2026-11-26): Build PASS · Warnings: 0 · Tests: 876 (875 pass, 1 skipped for open finding ZA-118), SQL Server (LocalDB), PostgreSQL and Redis; `/audit changed` run on the phase. ZA-012, ZA-018, ZA-077, ZA-099, ZA-117, ZA-144, ZA-145, ZA-146, ZA-157, ZA-159 fixed; ZA-162 found and fixed; ZA-163 new (Backlog). Next free ID: ZA-164
After Roadmap Phase 10a (2026-11-26): Build PASS · Warnings: 0 · Tests: 837 (836 pass, 1 skipped for open finding ZA-118), SQL Server (LocalDB), PostgreSQL and Redis; `/audit changed` run on the phase (one reviewer: its two Mediums — a request id that is not ours threw, `RunAs` reached other circuits' event handlers — and its Lows fixed). ZA-093 fixed, ZA-140 closed (D27), ZA-152 resolved. Next free ID: ZA-162
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

## Fixed, resolved, closed and declined

- **Roadmap Phase 10f-1 (2026-11-27, hooks and deletes).** Release notes in the Roadmap's "Phase 10f-1". Each test
  was seen failing with its fix reverted (controls pass both ways). Full suite 1205 (all pass, none skipped) on
  SQLite, PostgreSQL, SQL Server LocalDB and Redis; Release build 0 warnings; template smoke test passes; checked on
  the dev site (a resized image's copy written to its image's cache folder).
  - **ZA-163 (Low) — fixed (D62).** New discovered `IAfterEntityCommit<T>`: `SaveChangesAndLog` runs it right after
    the save when no transaction is open, otherwise the `AfterCommitHooks` transaction interceptor (added by the three
    DbContexts) runs it when the caller's transaction commits and drops it on a rollback, failed commit or dispose.
    A throwing hook is logged and the others run. `IAfterEntitySave<T>` unchanged. Tests:
    `Shared/AfterCommitHookTests` ×8 on three databases (a media purge whose file can't be removed is rolled back and
    never reaches the hook; the deferral reverted made four fail).
  - **ZA-155 (Low) — fixed (D63).** The resizer's cache is wrapped (`ResizedImageCache`) so each image's copies sit in
    a folder named by a hash of its path; `DiskStorageProvider.DeleteFile` removes it, `SaveFile` over the same name
    removes the old copies. Tests: `Media/ResizedCopiesTests` ×4 through ImageResize's real resizer (with the removal
    reverted the purge and overwrite cases fail).
  - **ZA-193 (Low) — fixed.** `DeleteDomainAsync` deletes the domain's redirects as tracked rows in the same change
    (audit row each), so the hooks see them, a veto keeps everything, and the `SeoRedirect` cache is cleared. Tests:
    `Seo/RedirectDomainTests` ZA193 ×2 (both fail with the database cascade left to do it).

- **Phase 11 audit follow-ups (2026-11-27, the owner's answers to the audit's two questions).** Release notes in the
  Roadmap's "Phase 11 audit follow-ups". Each test was seen failing with its fix reverted. Full suite 1175 (all
  pass, none skipped) on SQLite, PostgreSQL, SQL Server LocalDB and Redis; Release build 0 warnings; checked on the
  dev site (the jobs' "off" log lines, the sign-out POST 400 without a token).
  - **ZA-185 (Medium) — behaviour fixed (D60; the audit had documented it).** `AddZauberCms` registers MVC's view
    features (`AddMvcCore().AddViews()`, no Razor view engine), so a site's or plugin's controller with
    `[ValidateAntiForgeryToken]`/`[AutoValidateAntiforgeryToken]` answers 400 without a token instead of 500, and
    `[IgnoreAntiforgeryToken]` opts an action out. The "documented only" wording in the 11e release notes,
    FrontEnd.md and AGENTS.md is replaced. Test: `Bootstrap/SiteControllerAntiforgeryTests` ZA185 ×2, over HTTP on
    Kestrel with a test controller (400 without a token, 200 with one; 500 with the registration reverted).
  - **ZA-198 (Low) — fixed (D61).** `Zauber:Jobs:DailyCleanup` and `Zauber:Jobs:SitemapGenerator` (default `true`, in
    the three shipped appsettings files): a job that is off logs one line when the site starts and returns, so one
    server of a web farm can run the daily clean-up. The other hosted service (the Redis cache-clear listener) and
    the admin culture circuit handler are not jobs and have no switch. Test: `Bootstrap/BackgroundJobsTests` ZA198
    ×8 (defaults, the registered hosted services are exactly the two jobs, off finishes at once, on keeps running;
    the daily job kept running with its check reverted); `AppSettingsParityTests` pass with the new keys.

- **Phase 11 audit (2026-11-27, `/audit changed` over 2f4dc41..773505c, mode fix).** Release notes in the Roadmap's
  "Phase 11 audit" notes. Every fix has a regression test that was seen failing with the fix reverted (the two
  ZA-176 controls pass both ways, as they should). Full suite 1165 (all pass, none skipped) on SQLite, PostgreSQL,
  SQL Server LocalDB and Redis; Release build 0 warnings.
  - **ZA-169 (Medium) — fixed.** Under nested domains (`example.com`, `example.com/fr`) `RedirectMiddleware` took a
    request to be on both, so the shorter domain's redirect won on `/fr/…` and a loop across the two was saved and
    bounced visitors (reproduced). The request is now on the longest matching domain (`DomainMatching.FindDomain`,
    as content routing); `RedirectRules.FindLoop(redirect, others, domains)` follows the chain per host and works out
    the domain at each step, and `SeoService.SaveRedirectAsync` passes the domains (the old overload stays). Tests:
    `Seo/RedirectDomainTests` ZA169 ×5.
  - **ZA-170 (Medium) — fixed.** A domain saved as `https://example.com`, `example.com.`, `example.com/` or
    `example.com:8080` (the dialog's own example) matched nothing; since 11a its redirects never fired (reproduced).
    `DomainMatching.Normalize` (internal) at match time; stored rows unchanged; a domain with a port matches only that
    port. The dialog's help text says so. Tests: ZA170 ×4.
  - **ZA-171 (Medium) — fixed.** `RenderSeoTags` declared a content-finder page's stored URL as canonical and
    `og:url` (in FrontEnd.md's own example a 404). It reads the cascaded `EntryModel` and uses the address asked for
    without its query string. Tests: `Seo/SeoTagsTests` (rendered with `HtmlRenderer` on a real site).
  - **ZA-176 (Medium) — fixed.** `AddZauberCms(app)` read the options (validation) before writing the bootstrap's
    warnings, so a failing setting lost them and reached only stderr; and a plugin built against 4.x whose public type
    implements a contract moved in 11e made `GetExportedTypes` (discovery, Blazor's router) throw a `TypeLoadException`
    naming neither (reproduced with a run-time-built assembly). Now the warnings are written first, a failing setting
    is logged as critical (`ZauberCMS.Startup`) and rethrown, and `ExtensionManager.EnsurePublicTypesLoad` stops the
    start naming the assembly and the type; non-public unloadable types still only warn. Tests:
    `Bootstrap/StartupDiagnosticsTests` ZA176 ×4; a template site with a bad culture wrote the critical line to its log.
  - **ZA-177 (Medium) — fixed.** `release.ps1` had a literal TAB in the template site's path (from 9234170): the
    script stamped two files and stopped (reproduced on a copy). Test: `BuildBaselineTests` ZA177 (every stamped path
    exists, no control characters).
  - **ZA-178 (Medium) — fixed.** The publish job did not check that HEAD contains the newest version tag, so rerunning
    an older run after a newer release published older code under a newer version. It now fails unless
    `git merge-base --is-ancestor <newest tag> HEAD`; a rerun of the tagged commit is unchanged. Checked with a scratch
    repository and bare origin (9 cases; before the fix two published old code); `actionlint` clean. Test:
    `BuildBaselineTests` ZA178. The 15 existing tags are all on `main-static`.
  - **ZA-185 (Medium) — fixed by documentation; behaviour is an owner question.** Since 11e (`AddControllers`) a
    site's or plugin's `[ValidateAntiForgeryToken]`/`[AutoValidateAntiforgeryToken]` action answers 500 (reproduced
    on the real bootstrap; `AddMvcCore().AddViews()` restores it). The 11e release notes and FrontEnd.md now say so.
  - **ZA-172, ZA-173, ZA-174 (Low) — fixed.** Content finders are disposed after the lookup; a lookup from inside a
    finder does not run the finders again (it recursed 25 levels in a throwaway test) and `EntryPage` passes the
    request's token; a finder language that is not a culture is ignored with one warning, and the XML doc and
    FrontEnd.md say the finder's language wins. Tests: `Content/ContentFinderTests` ZA172–ZA174.
  - **ZA-175 (Low) — fixed.** `GetCachedDomainsAsync` (now per request with domain redirects, per miss with finders)
    built a DbContext and rendered SQL for the key on every hit; one fixed key under the `Domain` prefix, which domain
    saves clear. Test: ZA175.
  - **ZA-179, ZA-180, ZA-181, ZA-182, ZA-183 (Low) — fixed.** The `AdminDefaultLanguage` error gives the runtime's
    reason and names invariant-globalization mode (was "'en-US' is not a culture name such as en-US"); NU1900 is not
    a build error; the smoke test prints the site's stderr and log tail on a failure (normal run passes on Windows
    PowerShell 5.1); `FluentValidation.IValidator`1` is in `ExtensionManager.DiscoveryInterfaces` (a non-public
    validator is a start-up warning); `NoCacheInvalidationBus` and `RedisCacheInvalidationBus` are public (D53),
    snapshot updated. Tests: ZA179 ×2, ZA180, ZA182, the public-types snapshot.
  - **ZA-184, ZA-186 (Low) — fixed (docs).** The import comment said a content-type import cannot be left half done
    (two saves); AGENTS.md said there is no content-finder pipeline; the audit runbook pointed at `Core/Modal`; the
    11d, 11e and 11f release notes missed list layering across configuration sources, the blank provider, the
    antiforgery attributes and the replacement for `AssemblyManager.Assemblies`. Services.md and Standards.md
    describe redirect domains, list layering and the blank provider.
  - Dropped or downgraded on verification: a `www.` mismatch and upper-case domains (match as designed); the stale
    redirect cache after a domain delete is a nit (recorded with the hooks gap as ZA-193); the registration page's
    not-found layout is older than Phase 11 (ZA-194).

- **Phase 11g (2026-11-27): content finders, the service split, custom components, the DbContext pattern
  (Roadmap D55–D59).** Details and the release notes in the Roadmap's Phase 11g notes.
  - **Content finders (owner request, D55) — added.** `IContentFinder`, discovered, run in
    `IContentService.GetContentFromRequestAsync` after the URL lookup finds no live page; the page is loaded again
    under the live rule. Verified by `Content/ContentFinderTests` (9: hit, miss, order, a throwing finder and
    constructor, unpublished / recycled / unknown pages, cache, language, home page) and on the dev site with a
    scratch finder (200 with the route values in the view, a throwing finder logged and the address 404).
  - **ZA-125 [Low] Very large classes; `Content.Url` obsolete with no exit — fixed.** `ContentService` and
    `MembershipService` split into partial-class files by concern (D56; same types, constructors and members,
    pinned by `CoreSurfaceTests`). `Content.Url` is the stored URL and not obsolete; the `Url()` extension is
    obsolete with the exit in its message (D57); callers migrated, the pragmas for it removed, no schema change.
    Verified by the build (no CS0618 suppressions left for it), `CoreSurfaceTests` and the full suite.
  - **ZA-126 [Low] Interactive custom components receive the whole `Content` graph — fixed.**
    `ICustomContentComponent.ContentId` replaces `Content`; `RenderCustomContent` passes only the id (D58). Verified
    by `Content/CustomContentComponentTests` (HtmlRenderer on a real site) and on the dev site: the contact form's
    prerender descriptor 7046 → 777 characters, the form filled in and sent in a browser.
  - **`IDbContextFactory` (known deviation in Standards.md) — closed, not adopted (D59).** The scope per operation
    picks the provider per scope (installer, no-database start) and shares its context with Identity's stores;
    the deviation row is gone and the pattern is written down in Standards.md §3 and Services.md.
- **Phase 11f (2026-11-27): public surface and API conventions (Roadmap D53–D54).** Details and the release notes
  in the Roadmap's Phase 11f notes.
  - **ZA-092 [Medium] Core exposes hundreds of public types — fixed.** The rule for what is public is written down
    (Standards.md §1, D53: what a site builds on stays public, service implementations included, as in Umbraco;
    EF mappings, migrations, design-time factories, wired middleware, ZauberCMS's own hosted services, circuit
    handlers and start-up plugins, and plumbing are internal; no `InternalsVisibleTo` between packages). Public
    types, from the assemblies' metadata: Core 429 → 375, Components 278 → 274, Routing and ZauberCMS unchanged
    (Razor components only). `AssemblyManager` is internal without its unused public mutable caches; three unused
    public types are `[Obsolete]` (D50). `migrations.ps1` makes each new migration internal. Verified:
    `Bootstrap/PublicApiTests` (the public types equal `Bootstrap/Snapshots/PublicTypes.txt`; no public plumbing
    kind; every built-in extension type public; EF lists exactly the internal migrations and applies every internal
    mapping on SQL Server, SQLite and PostgreSQL), the script run on all three providers (an empty migration came
    out internal, then removed), `dotnet ef migrations list`, the full suite, the template smoke test and the dev
    site.
  - **ZA-124 [Low] API convention drift — fixed.** D54, Standards.md §11: `QueryContentParameters` has
    `IncludeDeleted`/`OnlyDeleted` with `IsDeleted` as an `[Obsolete]` alias (defaults unchanged); the `Cached`
    defaults stay, under a written rule, each pinned; `AddAudit` takes `CancellationToken cancellationToken =
    default` instead of `CancellationToken?`, `Export/ImportContentTypeAsync` take and forward a token,
    `SaveChangesAndLog` and `IQueryModel.ExecuteQuery` default theirs; discovery interfaces stay where they are, by
    feature, with the convention for new ones. Verified: `Bootstrap/ApiConventionTests` (recycle-bin defaults and
    names, `Cached` defaults, token shape over every public method, a token on every service interface's async
    method) and `ContentSoftDeleteTests` (the new flags and the alias for `false`, `true`, `null`).
- **Phase 11e (2026-11-27): the MVC path removed, UI contracts out of Core (Roadmap D51–D52).** Details and the
  release notes in the Roadmap's Phase 11e notes.
  - **ZA-090 [Medium] Unwired MVC rendering path ships as public API — fixed.** Removed (not made obsolete, D52):
    `IContentFinder`, `ContentFinderPipeline`, `DefaultContentFinder`, `ZauberRouteValueTransformer`,
    `ZauberCMS.Core.Rendering`, `HtmlHelperExtensions`, `RenderExtensions`, Routing's `Views/` and view components,
    `IContentBlockView.RenderMode`, the commented-out routes, their registrations and Routing's
    `AddRazorSupportForMvc`. Nothing live used them (grep over every project, the template, tests and docs;
    `IContentFinder` was never a listed extension point). `AddControllersWithViews` became `AddControllers`; a
    before/after list of the bootstrap's registrations showed 51 MVC view services gone, so `IMemoryCache` and a
    memory `IDistributedCache` are registered explicitly, and the sign-out POST checks its antiforgery token itself
    (`[ValidateAntiForgeryToken]` needs the view services: it answered 500 without them, found by the new test).
    Verified: `Bootstrap/CoreSurfaceTests` ZA090 (no MVC types or compiled views, the caches, Redis's cache kept,
    the sign-out over HTTP: 400 without, 302 with the token; 500 with the attribute back), the full suite, the
    template smoke test, and the dev site (front-end pages, blocks, 404, media resize, account pages, admin, sign-out).
  - **ZA-091 [Medium] Core's public contracts depend on Radzen and Blazored.Modal types — fixed.** `IContentProperty`,
    `ISectionNavGroupAction`, `DialogExtensions`, `NotificationServiceExtensions`, `TreeExtensions`,
    `GetPropertySettings(this IContentProperty)` (now `ContentPropertyExtensions`), `ValidateService<T>`,
    `MediaValidationService` and the vendored modal moved to ZauberCMS.Components under their old full names (D51);
    their registrations moved to the Components start-up plugin `AdminUiStartup`; Radzen.Blazor moved from Core's
    package references to Components'. Verified: `Bootstrap/CoreSurfaceTests` ZA091 (Core references neither Radzen
    nor Components and has no `Blazored*` type, the 16 moved names in Components, the services registered, discovery
    finds the editors and group actions), `PersistedTypeNameTests` unchanged, the full suite, the template smoke
    test, and the dev site in a browser (a block's side panel with the modal's script and CSS from Components, the
    group "…" actions, a Radzen notification).
- **Phase 11d (2026-11-27): start-up and DI (Roadmap D48–D50).** Details in the Roadmap's Phase 11d notes. Also
  fixed: the CI run of 11c (2cd1fb6) failed on Linux, `PackageContentsTests.ZA119…` built the icon's path with the
  `\` of the MSBuild item, which on Linux is part of a file name; the test now turns it into the platform's separator.
  - **ZA-120 [Low] Discovery and configuration fail silently or late — fixed.** Settings are validated at start-up
    (`ZauberSettingsValidator`, `ValidateOnStart`, and read first in `AddZauberCms(app)`): a `Zauber:Plugins:*`
    name that no discovered provider has (it threw `KeyNotFoundException` at first use) or an
    `AdminDefaultLanguage` that is not a culture stops the start with the setting, the value and the providers
    found; `ProviderService` gives the same message. Settings that do nothing as written (a `SiteUrl` that is not
    an http(s) address, a fallback view or account layout that is not found) are start-up warnings. The bootstrap
    collects what it skips (types that cannot load, an `IStartupPlugin`/`IExternalAuthenticationProvider` without
    a parameterless ctor, a non-public extension type in a site or plugin assembly) in `StartupMessages`, which
    `AddZauberCms(app)` logs (category `ZauberCMS.Startup`); a bootstrap ctor that throws names its type. The type
    cache was keyed by `T` alone: a call with a predicate, or for non-public types, got the dictionary of an
    earlier call, and `SetAssemblies` never cleared it (a second host in one process with other assemblies saw the
    first one's types); now keyed by (type, exported only), only searches without a predicate are cached, the
    caches are cleared by `SetAssemblies`, and callers get a copy. Verified: `StartupDiagnosticsTests` (ZA120:
    mistyped storage and email provider, culture, defaults/installer/blank provider pass, the three shipped
    `appsettings.json` files pass with no warning, the warnings, the runtime message, the collected and logged
    warnings, the interface list, the cache); the cache test failed with the old cache key. Dev site: a mistyped
    `Zauber__Plugins__StorageProvider` stops the start with the message; `Zauber__SiteUrl=www.example.com` logs
    the warning; the shipped settings log no warning.
  - **ZA-121 [Low] DI leftovers — fixed.** Removed the scoped `HttpClient` (base address from the request's Host
    header; an injected `HttpClient` is now the framework's default client from `AddHttpClient`), the
    `IUserEmailStore<User>` registration (`UserEmailStore` `[Obsolete]`), and the second `SignInManager`
    registration (`AddSignInManager<ZauberSignInManager>()`). `IdentityUserAccessor` and `ContentNavGroup.Actions`
    are `[Obsolete]`; the accessor stays registered. Every caller was grepped (Core, Components, Routing, ZauberCMS,
    Web, Template, tests): none. Verified: `StartupDiagnosticsTests.ZA121…` (one `SignInManager<User>`, the same
    instance as `ZauberSignInManager`; failed with the old double registration).
  - **ZA-167 [Low] A list setting adds to its defaults — fixed.** `ZauberSettingsBinding` reads the section for the
    bootstrap and for `IOptions<ZauberSettings>`: after binding, every list the configuration sets replaces the
    property (the binder appends to a pre-filled list); a list it does not set keeps its default; dictionaries are
    left merged. Verified: `StartupDiagnosticsTests.ZA167…` (`["uploads"]` replaces the default in `ZauberSettings`
    and in ImageResize's options; the shipped three roots once each; a guard over every list setting with a
    default), all three failed without the replacement; dev site resizes a `/media` image.

- **Phase 11c (2026-11-27): packaging and the template (Roadmap D45–D47).** Details in the Roadmap's Phase 11c
  notes. Every package was packed from the previous commit and from this one and the contents compared.
  - **ZA-087 [Medium] Components package leaks npm manifests — fixed.** `package.json` / `package-lock.json` are
    `None` items (were content: packed as `content/` and `contentFiles/` into every site); the machine-specific
    exclusion is gone. Verified: the package's file list and nuspec before and after (both files and the
    `contentFiles` section gone); `PackageContentsTests.ZA087…` (failed on the previous commit).
  - **ZA-094 [Medium] Template pins an SDK-internal asset package — fixed.** The template site sets
    `RequiresAspNetWebAssets` instead of pinning `Microsoft.AspNetCore.App.Internal.Assets` 10.0.0; the SDK
    restores the version it ships (10.0.15 with SDK 10.0.404, seen in the site's `project.assets.json`). Verified:
    the template smoke test (the published site contains `_framework/blazor.web.js`, the fingerprinted and plain
    addresses answer 200 with the 200 KB script); `PackageContentsTests.ZA094…`.
  - **ZA-118 [Low] Packaged `appsettings.json` has no `Zauber:ImageResize` section — fixed.** Added, the same as the
    template's. Verified: the parity test that waited for it is un-skipped and passes.
  - **ZA-119 [Low] Package metadata gaps and csproj cruft — fixed (D45).** Readme (`ReadMe.md`), icon (the
    template's existing logo), Source Link, `.snupkg` symbol packages, deterministic CI builds, all in
    `Directory.Build.props`; `CopyNuGetPackage` and CI handle the `.snupkg`. `NoWarn CS0618` removed from Components
    (one use, the content editor's URL field, suppressed locally with the reason). Components, Routing and
    ZauberCMS no longer repeat Core's packages; dead csproj entries removed. Verified: nuspecs before and after
    (`<readme>`, `<icon>`, fewer dependencies), the Core `.snupkg` PDB holds the Source Link map to the commit,
    Release build 0 warnings, the smoke test loads every admin script and style (Radzen, BlazorMonaco, Components)
    through the shorter dependency lists; `PackageContentsTests.ZA119…` (three tests).
  - **ZA-123 [Low] Template and sample rough edges — fixed (D47).** Launch profiles `https` / `http` (was
    "Umbraco.Web.UI"); `template/.gitignore` added and packed (`NoDefaultExcludes`); `/Home/Error` was already gone
    (Phase 10e). Verified: the smoke test finds the `.gitignore` in a new site and no "Umbraco" profile;
    `PackageContentsTests.ZA123…`.
  - **ZA-168 [Low] Shipped logging settings broken on Linux — found and fixed.** `Logs\log.txt` made a file named
    `Logs\log<date>.txt` in a Linux site's content root; the `WithThreadId` / `WithMachineName` enrichers had no
    package and were skipped. Now `Logs/log.txt` and `FromLogContext`, the same in the three `appsettings.json`
    files. Verified: `AppSettingsParityTests.ZA168…` (failed on the previous commit); the smoke test's log check
    on Linux (PowerShell 7 in `mcr.microsoft.com/dotnet/sdk:10.0`) and Windows; the dev site writes `Logs/`.
  - **Template smoke test (Phase 2, deferred here) — built (D46).** `tests/template-smoke.ps1`, a CI step in the
    build job. Verified: passes on Windows PowerShell 5.1 and in the Linux SDK container with PowerShell 7.6
    (44 checks, 54 s with an empty NuGet cache); afterwards the NuGet cache, the `bin` folders and the machine's
    installed templates were as before. Not run on GitHub yet.
  - **ZA-167 [Low] new, open (Phase 11d):** `ImageResize:ContentRoots` is appended to its defaults by the binder.

- **Phase 11b (2026-11-27): engineering baseline, CI and release hardening (Roadmap D43–D44).** Details in the
  Roadmap's Phase 11b notes.
  - **ZA-131 [Low] Engineering baseline still absent — fixed (D43).** Root `Directory.Build.props` (framework,
    nullable, analysers at `AnalysisLevel` 10.0 with Recommended Globalization/Performance/Reliability/Security/Usage
    rules, warnings as errors except NU1901–NU1904, NuGet audit, the four packages' one version and metadata, the
    `CopyNuGetPackage` target), `Directory.Packages.props` (central package management), `.editorconfig`,
    `.gitattributes` (`* text=auto`: renormalising changes no tracked file), `global.json` SDK pin (10.0.400,
    `latestFeature`), and a CI scan that fails on vulnerable packages, direct or transitive. `ZauberCMS.Template/`
    has its own stop files, so the template package and site are untouched. About 100 analyser hits fixed (culture
    in keys, attributes and parsing, ordinal comparisons, `Count`/`Length`, statics, `Regex.Count`, exception
    types), a few suppressed at the site with the reason. Verified: Release build 0 warnings (also
    `--no-incremental`); the four packages and the template packed before and after have the same nuspecs and
    file lists (sizes equal except the assemblies); `dotnet msbuild -getProperty` shows the template site outside
    the root settings; `Packaging/BuildBaselineTests` and `Shared/CultureInvariantTests` (the two culture tests
    failed on the old code: "1.5" read as 15 under de-DE, a soft hyphen dropped the first letter).
  - **ZA-089 [Medium] CI can publish one version built from two commits — fixed (D44).** The publish job pushes
    the version's tag before it builds; a tag on another commit, or one pushed meanwhile, stops the run. Packages
    are checked and pushed in dependency order; `--skip-duplicate` only on a rerun of the tagged commit. Verified:
    the version, stamp and commit-back steps run as written against a scratch repository and bare origin (first
    release, rerun, partial failure then a new commit gets the next version, tag pushed between fetch and push
    rejected, among 13 cases); actionlint 1.7.12 with shellcheck 0.11.0 clean; `BuildBaselineTests` (tag before
    build and pushes, dependency order). CI itself not run: it runs on GitHub only, and nothing was pushed to
    `main-static`.
  - **ZA-109 [Low] CI and release script hardening — fixed.** Workflow `permissions: contents: read`, `contents:
    write` only for publishing; actions pinned by commit (checkout v7.0.1, setup-dotnet v6.1.0) with the build
    job's checkout keeping no credentials; `paths-ignore` for documentation; prerelease-safe parsing (`X.Y.Z-label`
    published once, a non-semver version fails); `release.ps1` stamps `Directory.Build.props` and the template's
    csprojs keeping each file's bytes and encoding (no blank line per run; the 40 it had added removed). Verified:
    the scratch-repository runs above (prerelease, second push of a prerelease, non-semver), actionlint, and
    `release.ps1`'s functions against copies of its three files (round trip byte for byte).
- **Phase 11a (2026-11-27): the not-found page and redirect domains (Roadmap D41–D42).** Details in the Roadmap's
  Phase 11a notes.
  - **ZA-164 [Medium] The front end's "not found" page is sent with an empty body — fixed.** Reproduced with curl
    on the dev site (`/nope-page`: 404, `Content-Length: 0`) and in a bare .NET 10 Blazor app: .NET 10's
    `RazorComponentEndpointInvoker` drops the HTML of a static render that ends at 404 (not re-executed, no
    `NotFoundPage`), for status-code pages. New `HttpContext.SetStatusCodeWhenResponseStarts` sets the status from
    `Response.OnStarting`, and only on a response still at 200; `EntryPage` sets 404 for any not-found view, and
    `NotFound404` and the closed registration page use it. Verified: `Bootstrap/NotFoundStatusTests` over real HTTP
    (Kestrel), and curl on the dev site in Development and Production (`/nope-page` 404 with the view's HTML).
  - **ZA-165 [Low] A redirect's domain is ignored — fixed (D42).** `RedirectMiddleware` applies a redirect with a
    domain only on requests on that domain (the domains read from the cached list, only when a redirect has one);
    for the same address the domain's redirect wins over one for all; `RedirectRules.FindLoop` follows the same
    rules. Verified: `Seo/RedirectDomainTests` (middleware on the real bootstrap, domains saved through
    `SaveDomainAsync`).
  - **ZA-166 [Low] A domain's path matched characters, and the first of two matching domains won — found and
    fixed.** `example.com/en` matched `/english`; with `example.com` and `example.com/fr`, the list order decided.
    New internal `DomainMatching` (whole segments, longest domain) is used by content routing and redirects.
    Verified: `Seo/RedirectDomainTests` ZA166 cases.

- **Owner decisions (2026-11-27): no Backlog.** Its items moved to Roadmap Phases 10f (ZA-155, ZA-163), 10g
  (back-office roles, D38), 10h (ZA-129, D37) and 10i (ZA-128).
  - **ZA-151 [Low] The installer's pages cannot be restyled or extended — declined (D39).** The owner does not want
    it; `IInstallService` stays replaceable.
  - Phase 7's open questions closed as built; the installer keeps writing `appsettings.json` (D40).

- **Phase 10e (2026-11-27): front end, security and identity hardening (Roadmap D33–D36).** Checked in a browser on
  a scratch copy of the sample site in Production mode unless a line says tests only; details in the Roadmap's
  Phase 10e notes.
  - **ZA-095 [Medium] Admin and reserved paths matched as a bare prefix — fixed.** `SitePaths.IsSystemPath`
    (whole segments; `/_blazor` added). `/administration` rendered the admin before (200, seen on the previous
    commit) and is an ordinary page now.
  - **ZA-098 [Medium] No working front-end error page — fixed (D33).** Outside Development: the content page at
    `Zauber:ErrorPagePath` (default `/error`), else the built-in page, both with 500; seen for a throwing GET and a
    throwing form post, with and without an `/error` page. Found on the way and fixed: the culture and redirect
    middleware caught a page's exception and ran the page a second time.
  - **ZA-116 [Low] "?" made a redirect a regex; no loop guard — fixed (D34).** Nullable `IsRegex` column (three
    migrations; old rows keep the old guess), a switch in the dialog, loops refused on save in the middleware's
    order, no redirect of an address to itself. Seen: an exact `/old-search?q=1` redirect and a refused loop.
  - **ZA-033 [Low] Deleted users kept their personal data — fixed (D35).** Deleting erases the person
    (`UserAnonymiser`); refused for your own account and the last administrator. Seen: an admin delete left
    `deleted-<id>` with no email or password and an audit row without personal data. Users deleted before v5 are
    left as they are (owner: no erase on upgrade).
  - **ZA-104 [Low] Image resizing upscaled; cache unbounded — fixed (D36).** `AllowUpscale` false, `MaxCacheBytes`
    2 GB (tests only).
  - **ZA-150 [Low] A form-protection token could be reused for 12 hours — fixed.** `TryUseToken` and
    `FormProtectionResult.AlreadyUsed`, remembered by the decrypted value. Seen: a second visitor sending a used
    token was refused and given a new one.
  - **ZA-106 [Low] Login revealed unconfirmed accounts and resent without limit — fixed.** Lockout, then password,
    before "isn't confirmed"; resend at most every 10 minutes (tests only).
  - **ZA-030 [Low] Logout by GET from another site — fixed.** Auto-submit only for `Sec-Fetch-Site`
    same-origin/none or a same-host Referer; the page cannot be framed. Seen over HTTP for both headers.
  - **ZA-105 [Low] `ExternalLoginAsync` created accounts without the checks — fixed.** It no longer creates
    accounts (code read; no shipped caller).
  - **ZA-107 [Low] Email addresses in logs — fixed.** User ids instead (grep of every log call).
  - **ZA-031 [Low] MD5 — fixed.** SHA-256 for generated user names; `ToMd5`/`GetMd5` obsolete.
  - **ZA-032 [Low] Placeholder email credentials in the shipped settings — fixed.** Empty values; the SMTP
    provider names the missing key (test reads the three files).
  - **ZA-149 [Low] Rate limits fixed in code — fixed.** `Zauber:Identity:LoginRateLimit` / `RegisterRateLimit`.
  - **ZA-110 [Low] Restricted-page gate relied on `NavigateTo` throwing — fixed.** Access decided before the view.
  - **ZA-111 [Low] Hosted services and middleware ignored cancellation — fixed.** `DailyJob` as a
    `BackgroundService`, the sitemap's token and atomic write, `RequestAborted` in the middleware.
  - **ZA-114 [Low] Layout culture; sitemap dropped the home page — fixed.** `AdminCulture.Apply` in `BlankLayout`;
    a `Guid.Empty` redirect on the sitemap root is ignored.
  - **ZA-115 [Low] Smaller performance items — fixed.** Generated regexes, stable paging orders, one query for a
    unique URL, the redirect list cleared on every server, one shared load per cache key across requests.
  - Review (`/audit changed`, two reviewers): three Highs found in the phase's own changes (password guessing on
    unconfirmed accounts, the single-load cache scoped per request, a 404 that counted as the error page) and their
    Mediums and Lows, all fixed before the commit; noted: slow regex redirects, per-server throttles.
  - New: **ZA-164** (the front end's 404 page has an empty body; there on the previous commit too) and **ZA-165** (a
    redirect's domain is ignored), both in Phase 11.
- **Phase 10d (2026-11-27): admin UI robustness (Roadmap D32), and the owner's resizable tree panel.**
  - **ZA-097 [Medium] No `ErrorBoundary` anywhere — fixed (D32).** `AdminErrorBoundary` round each property editor,
    dashboard tab, the navigation and the page body; a plain boundary round each side panel's content. "Try again",
    the message only with detailed errors on, the exception logged.
  - **ZA-101 [Medium] The admin prerendered — fixed.** `InteractiveServerRenderMode(prerender: false)` for `/admin`.
  - **ZA-096 [Medium] Admin scripts after enhanced navigation — fixed.** `AdminAccessMiddleware` drops the
    `blazor-enhanced-nav` response header on `/admin`, so blazor.web.js loads the admin as a full page (checked in a
    browser: Radzen and Sortable present after a link from the home page).
  - **ZA-108 [Low] Sortable.js from a CDN without SRI; scripts not fingerprinted — fixed.** Sortable.js 1.15.7 ships
    in the Components package; every admin script and editor asset goes through `@Assets`.
  - **ZA-085 [Low] SEO checker blocked the circuit — fixed.** The sync HTTP had gone with ZA-019; a run is now
    cancelled on leaving, re-running, Stop or after 3 minutes, and a check that throws is shown as failed.
    `ISeoCheck` has a cancellable default method.
  - **ZA-156 [Low] Media bin stopped at 500; a binned folder did not open — fixed.** Both bins page through
    `InRecycleBin` tree levels and open a binned page or folder.
  - **ZA-161 [Low] "Show more" dropped the highlight — fixed.** The row is a button that never selects the node.
  - **ZA-025 [Low] Duplicate tree alias constant — fixed.** `StructureElementTypeTree` has its own value
    (release note: a menu for one structure tree no longer shows on the other).
  - **ZA-026 [Low] `<RadzenComponents/>` twice — resolved:** already fixed by ZA-138 (Phase 4); `SectionLayout` has
    one.
  - **ZA-160 [Low] `JSDisconnectedException` on a media folder reload — resolved (not reproduced).** Four full reloads
    of a 105-item folder page logged nothing; our only component there that calls JavaScript on dispose catches the
    disconnect. Reopen with a stack trace if seen again.

- **Phase 10c (2026-11-26): property editors and stored values (Roadmap D30, D31).**
  - **ZA-078 [Medium] Pickers lost the editor's order — fixed.** Front-end helpers and the editors put items back in
    the stored order (`InPickerOrder`); the editors load every picked item (the default page of 10 dropped the rest,
    and the next change saved without them) and save after a drag.
  - **ZA-029 [Low] One item stored as a bare id, several as JSON — fixed (D30).** One JSON array for any count, both
    shapes read everywhere (`ToPickerIds`, `ToValue<Guid>`, `ToValue<List<Guid>>`, the admin's single-id fields); no
    data migration.
  - **ZA-100 [Medium] A second file pick broke the first — fixed.** Files are copied to a server temp file when chosen
    (`StagedBrowserFile`).
  - **ZA-015 [Medium] `AddFluentValidation` never unsubscribed; a throwing validator ended the circuit — fixed.**
    Returns `IDisposable`; handlers catch and log, the form stays invalid; locked scan.
  - **ZA-027 [Low] Numbers in the current culture — fixed (D31)**, with dates (the long-date text also dropped the
    time): invariant on write, invariant then current culture on read.
  - **ZA-028 [Low] Repeated aliases threw — fixed (D31).** Content type save refuses clashes, through compositions
    too; reads take the first value.
  - **ZA-112 [Low] `SortableList` dropped callback tasks — fixed.**
  - **ZA-127 [Low] Component lifecycle roll-up — fixed** for the cited editors and the cases a survey of the admin
    found (14; 13 fixed, `EditContent`'s reload left on purpose). The worst: editor rows not keyed on the loaded
    copy, so after a version restore or moving to another user the editors kept the old values and saved them back.
  - Tests: `Content/PropertyValueTests.cs`, Components `Editors/StagedBrowserFileTests.cs`.
- **Phase 10b (2026-11-26): save hooks and content data (Roadmap D28, D29).**
  - **ZA-012 [Medium] Hooks saw `Unchanged`; a content type veto could not roll back — fixed.** `SaveChangesAndLog`
    runs the hooks for every row the save writes (change tracker, every entity type, each row's own state) and one
    veto stops the whole change; the early audit saves are gone (audit rows are staged and written by the change's
    save). Covers sort, move, restore, recycle, permanent delete, content types, copy, import and version delete.
  - **ZA-157 [Low] Media below an item and an emptied bin ran no hooks — fixed** by the same change; a veto on any item
    stops the bin, restore or purge.
  - **ZA-159 [Low] Blocks of Clear changes and version restore ran no hooks; drafts ran none — fixed.** Those rows reach
    the hooks; a "Save only" runs the `UnpublishedContent` hooks (D29).
  - **ZA-018 [Medium] `SaveTagItemAsync` skipped the hooks — fixed.** Links and audit rows in one hooked save.
  - **ZA-099 [Medium] The tags editor wrote to the database on every render — fixed.** The page save writes the tag
    links from the published values through the new discovered `ITagReferenceProvider`; drafts leave them, a permanent
    delete removes them.
  - **ZA-077 [Medium] Import over an existing alias orphaned the values — fixed.** Tab and property ids kept by alias.
  - **ZA-162 [Medium] Content type export always threw — fixed** (found by the ZA-077 test): it `Include`d the JSON
    tabs column.
  - **ZA-146 [Low] Two version writes at once lost one — fixed.** The page row is locked and the number read inside
    the transaction; the old flag is switched off as tracked rows, saved first.
  - **ZA-117 [Low] User save overwrote identity fields from a stale copy — fixed** (`MapProfileTo`).
  - **ZA-144 [Low] A draft kept the old URL after a restore — fixed.**
  - **ZA-145 [Low] Move and copy loaded the subtree twice — fixed.**
  - Tests: `Shared/SaveHookTests.cs`, `Content/ContentDataTests.cs` (with `ContentVersionRaceTests`),
    `Membership/UserSaveTests.cs`; new finding ZA-163 (Backlog).
  - `/audit changed` on the phase (one reviewer): its two Mediums (hooks twice for a caller's copy; no tag links for
    a block added in a draft and published later) and five Lows fixed with tests or code (see Roadmap Phase 10b).

- **Phase 10a (2026-11-26): who is calling (Roadmap D27).**
  - **ZA-093 [Medium] Write services need a Blazor component scope — fixed.** The services read the caller through the
    new, replaceable `ICurrentUserAccessor` (`RunAs`, then the Blazor authentication state, then the request's user,
    then nobody) instead of `AuthenticationStateProvider`; a write with nobody signed in is saved and records no user
    instead of throwing on `user!`. The installer runs the language and starter kit through `RunAs`. Tests
    `Membership/CurrentUserAccessorTests.cs`. Release note: six service constructors changed.
  - **ZA-140 [Medium] Services do no authorisation of their own — closed by owner decision D27.** Services trust their
    caller; the page, component or endpoint checks access (as in Umbraco and WordPress). No shipped path lets a
    visitor reach a write it shouldn't; the rule is documented in `AGENTS.md` and `Docs/Services.md` "Callers and
    access". Back-office roles other than Admin are in the Roadmap Backlog.
  - **ZA-152 [Low] The tests cannot see a scoping mistake in "run as the new administrator" — resolved.** The run-as no
    longer depends on a scope: `RunAs` holds for the install's async flow, and nothing else sets the test sign-in, so
    `The_starter_kit_runs_as_the_new_administrator` fails if the run-as is missing.
- **Reconciled 2026-11-26, after Phase 10 (findings assigned to phases).**
  - **ZA-113 [Low] EF used directly in a context menu, scope held across a dialog — resolved by 1b6f154.**
    `RestrictAccessContextMenu` now calls `IContentService.SetContentRolesAsync` after the dialog closes; it holds
    no scope or DbContext (fixed with ZA-073).
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
