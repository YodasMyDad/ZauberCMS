# ZauberCMS Audit Report

Last full run: 5668365 (2026-11-25, full) · Last changed run: 773505c (2026-11-27, Roadmap Phase 11, 2f4dc41..773505c) · Mode: fix
Build: PASS · Warnings: 0 (warnings are errors since Phase 11b) · Vulnerable packages: 0 · Outdated majors: 15 (the .NET 11 set, held by D4)
Next free IDs: ZA-199 (findings) · ZP-001 (proposals) · Umbraco reference: not yet compared (first run with lenses G and H to come)
After Roadmap Phase 10f-1 (2026-11-27): Build PASS · Warnings: 0 · Vulnerable packages: 0 · Tests: 1205 (all pass, none skipped), SQL Server (LocalDB), PostgreSQL and Redis · ZA-163 (after-commit hook `IAfterEntityCommit<T>`, D62), ZA-155 (resized copies removed with their image, D63), ZA-193 (a domain's redirects deleted through the change) fixed. Next free ID: ZA-199
After the Phase 11 audit follow-ups (2026-11-27): Build PASS · Warnings: 0 · Vulnerable packages: 0 · Tests: 1175 (all pass, none skipped), SQL Server (LocalDB), PostgreSQL and Redis · the owner's answers built: ZA-185 (antiforgery attributes on site controllers, D60) and ZA-198 (`Zauber:Jobs` switches, D61) fixed. Next free ID: ZA-199
After Roadmap Phase 11 (audit, 2026-11-27): Build PASS · Warnings: 0 · Vulnerable packages: 0 · Tests: 1165 (all pass, none skipped), SQL Server (LocalDB), PostgreSQL and Redis · `/audit changed` over the whole phase (three unit reviewers, two independent diff reviewers, two verifiers): nothing Critical or High from Phase 11; ZA-169…ZA-186 found and fixed (7 Medium, 11 Low), ZA-187…ZA-198 recorded (1 Medium, 11 Low, older than Phase 11). Next free ID: ZA-199. Earlier after-phase lines: [history.md](history.md) run log

> First full run. All findings come from reading code plus the deterministic checks below; **nothing was run in a
> browser or against PostgreSQL/Redis**, so items marked as needing a repro are reasoned, not observed. Critical and
> most High findings were re-read by the auditor before inclusion. No code was changed (report mode); five wrong
> claims in `AGENTS.md` / `Docs/` found by this run were corrected.

## Summary

| Severity | Open | New this run (Phase 11 changed run) | Fixed this run |
| --- | --- | --- | --- |
| Critical | 0 | 0 | 0 |
| High | 0 | 0 (ZA-187 was High, downgraded to Medium on verification) | 0 |
| Medium | 1 | 8 | 7 |
| Low | 11 | 22 | 12 |

Trend (first pattern counts, 2026-11-27, Phase 11 run): sync-over-async 48 · async-void 2 · fire-and-forget 7 ·
empty-catch 15 · unsanitised-markup 15 · raw-sql 1 · new-httpclient 0 · culture-unsafe-compare 6 (the suppressed EF
searches) · local-time 0 · regex-per-call 6 · count-gt-zero 0 · static-mutable 37 · event-subscribe 25 /
event-unsubscribe 22 · httpcontext-accessor 5 · exception-text 11. Flat against 2f4dc41 except regex 8 → 6 and
suppressed warnings 35 → 18; every hit on an added line is moved code or a suppression with its reason.

**Work order and owner decisions: [Docs/Roadmap.md](../Docs/Roadmap.md).** Every open finding is assigned there to
a phase (10f-2: ZA-187, ZA-189, ZA-190; 10g: ZA-188; 10h: ZA-129; 10i: ZA-128, ZA-194,
ZA-197; 12: ZA-191, ZA-192, ZA-195, ZA-196); there is no Backlog (owner, 2026-11-27). The run's two owner questions
were answered and built in the Phase 11 audit follow-ups: ZA-185 (D60) and ZA-198 (D61).
ZA-151 was declined (D39). What each phase fixed and how it was
verified: [history.md](history.md); the earlier after-phase status lines are in its run log.

## Open findings

### ZA-187 [Medium/High] The URL-to-page cache key holds the raw scheme, host, port and path
- Where: `ZauberCMS.Core/Content/Services/ContentService.Routing.cs` (`GetContentFromRequestAsync`, `RequestUrlForLookup`) · Category: A4/B2 · Since: 2026-11-27 (Phase 11 audit; code from Phase 10) · Roadmap: 10f
- Issue / Trigger: every Host header value (`AllowedHosts` is `*` in the shipped settings; host filtering ignores the
  port), port or spelling of a path (`/about/`, `/about//`) is a cache miss that loads the page whole and then a new
  entry holding its own copy of the page for up to about ten minutes. Only found pages are kept. Verified with a
  throwaway test (two hosts, one page: two entries). Was High; downgraded because entries expire and only hits stay.
- Fix: key on the matched domain id, the normalised slug and the flags (as the finder path does). · Test: same page,
  two hosts on no domain and two ports of one domain: one entry.

### ZA-128 [Low/High] Accessibility roll-up

- Where: `ZauberCMS.Components/Shared/TreeButton.razor:4`, `ZauberCMS.Components/Account/Pages/Login.razor:28-34` · Category: E · Since: 2026-11-25
- Issue / Fix: 8 of 38 icon buttons unlabelled; 7 clickable divs with no role or key handler (every tree button); ~139 Radzen inputs vs ~75 labels; `label for` without matching ids.

### ZA-129 [Low/High] Admin UI is not localised

- Where: `ZauberCMS.Components/` (166 razor files) · Category: E · Since: 2026-11-25
- Issue / Fix: 0 localiser uses, ~370 literal strings; `AdminDefaultLanguage` only affects formatting.

### ZA-188 [Low/High] A role-restricted page whose view is missing skips the role check
- Where: `ZauberCMS.Components/Pages/EntryPage.razor` (`OnParametersSetAsync`, the `MissingView` branch) · Category: D · Since: 2026-11-27 (code from Phase 10e) · Roadmap: 10g
- Issue / Fix: the shipped `MissingView` shows only the page's name, but a site's own gets the whole page. Run the
  role check before choosing the view.

### ZA-189 [Low/High] Only the newest 5000 redirects are compiled
- Where: `ZauberCMS.Core/Middleware/RedirectMiddleware.cs` (`GetOrCreateCompiledRedirectsAsync`) · Category: C · Since: 2026-11-27 (older) · Roadmap: 10f
- Issue / Fix: older redirects are ignored silently. Page through all of them, or log when the cap is hit.

### ZA-190 [Low/Medium] Domains are matched with `PathBase` on one side and without it on the other
- Where: `ZauberCMS.Core/Languages/ContentCultureCircuitHandler.cs` (`PageCultureAsync`), `EntryPage` vs `CultureMiddleware`/`RedirectMiddleware` · Category: C · Since: 2026-11-27 (older) · Roadmap: 10f
- Issue / Fix: a site under a path base gets different domains in a circuit and in the middlewares. Match on the
  path without `PathBase` everywhere.

### ZA-191 [Low/High] `GetContentFromRequestParameters.IgnoreInternalRedirect` is never read
- Where: `ZauberCMS.Core/Content/Parameters/GetContentFromRequestParameters.cs` · Category: F · Since: 2026-11-27 (older) · Roadmap: 12
- Issue / Fix: a public switch that does nothing. Honour it or make it `[Obsolete]`.

### ZA-192 [Low/High] Settings set in code do not reach the bootstrap's copy
- Where: `ZauberCMS.Core/ZauberSetup.cs` (`AddZauberCms(builder)`) · Category: F · Since: 2026-11-27 (older) · Roadmap: 12
- Issue / Fix: `Configure<ZauberSettings>` changes `IOptions<ZauberSettings>` but not the copy the bootstrap reads
  for ImageResize, Identity, rate limits and Redis. Document it, or read those from the options.

### ZA-194 [Low/High] With registration off, the not-found view is shown in the account layout
- Where: `ZauberCMS.Components/Account/Pages/Register.razor` · Category: E · Since: 2026-11-27 (older; visible since 11a sends the HTML) · Roadmap: 10i
- Issue / Fix: the not-found view renders inside the account layout, not its own. Render it as `EntryPage` does.

### ZA-195 [Low/High] CI's commit-back conflicts when two pushes are in flight
- Where: `.github/workflows/ci.yml` (publish job, commit-back) · Category: F · Since: 2026-11-27 (older) · Roadmap: 12
- Issue / Fix: the `concurrency` group serialises the publish jobs, but the second one's `pull --rebase` of the
  stamped version conflicts after its packages are pushed; the next push heals it. Stamp from the tag, or retry.

### ZA-196 [Low/High] A blank storage or email provider fails at first use without naming the setting
- Where: `ZauberCMS.Core/Providers/ProviderService.cs` and its ten callers (only `MediaService` checks) · Category: C · Since: 2026-11-27 (as before Phase 11d) · Roadmap: 12
- Issue / Fix: a blank `Zauber:Plugins:StorageProvider`/`EmailProvider` passes start-up and the first upload or email
  throws `NullReferenceException`. Throw a message that names the setting, or refuse blank at start-up.

### ZA-197 [Low/Medium] The vendored modal never unsubscribes its three events
- Where: `ZauberCMS.Components/Modal/BlazoredModal.razor` (`OnInitialized`, `DisposeAsync`) · Category: A4 · Since: 2026-11-27 (older, moved in 11e) · Roadmap: 10i
- Issue / Fix: `OnModalInstanceAdded`, `OnModalCloseRequested` and `LocationChanged` are never removed; harmless
  while the modal lives as long as the circuit, a leak if a layout re-creates it. Unsubscribe in `DisposeAsync`.

## Improvement proposals

None recorded yet: lenses G (editor UX) and H (Umbraco alignment) were added to the audit on 2026-11-27 and have
not run. The next `full` run (or `/audit G,H`) fills this section.

## Fixed this run / Resolved since last run

Since this run: Roadmap Phase 10f-1 (2026-11-27) fixed ZA-155, ZA-163 and ZA-193 (Low); details in
[history.md](history.md).

This run (Phase 11 changed run, 2026-11-27); details and how each was verified in [history.md](history.md):
- Medium: ZA-169 nested-domain redirects; ZA-170 domains with a scheme, port or trailing dot; ZA-171 canonical on
  content-finder pages; ZA-176 start-up failures lost from the log, unloadable plugin types unnamed; ZA-177
  `release.ps1` TAB; ZA-178 CI rerun of an older commit; ZA-185 antiforgery attributes without view services
  (documented by the run; the view features registered again in the follow-ups, D60).
- Low: ZA-172 finder disposal; ZA-173 finder re-entrancy; ZA-174 finder language; ZA-175 domain list cache hit;
  ZA-179 invariant-globalization message; ZA-180 NU1900; ZA-181 smoke test output; ZA-182 `IValidator<T>` in the
  discovery list; ZA-183 cache buses public; ZA-184 import comment; ZA-186 docs drift; ZA-198 background jobs per
  server (follow-ups, `Zauber:Jobs`, D61).

## Deterministic checks

| Check | Result |
| --- | --- |
| Build (`dotnet build ZauberCMS.sln -c Release`) | Phase 11 run: PASS, 0 warnings (warnings are errors). First run: 70 warnings, all `NU1902`/`NU1903` |
| Vulnerable packages | Phase 11 run: 0, direct and transitive. First run: 4 → ZA-102 |
| Outdated packages | Phase 11 run: HtmlSanitizer 11.0.26 → 11.0.74 and Radzen.Blazor 13.3.0 → 13.4.0 (no advisory); 15 majors, the .NET 11 set (D4). First run: see ZA-130 |
| Version alignment | OK — one `<Version>` 4.1.3 for the four packages, the template and its site match |
| Migration parity | OK — 5 migrations per provider, same names; Phase 11 changed only `public` → `internal` on them (no Up/Down change) |
| Config parity | Since Phase 11c the three `appsettings.json` files have the same `Zauber` keys and `Serilog` section (ZA-118, ZA-168; `AppSettingsParityTests`) |
| Docs drift | Paths all resolve; ~55 claims spot-checked, 5 wrong, corrected this run |
| Repo hygiene | Only `ZauberCMS.Web/app.db` tracked (exempt); `.gitattributes`, `.editorconfig`, `Directory.Build.props`, `Directory.Packages.props` and an SDK pin since Phase 11b (ZA-131 fixed) |
| Tests | Phase 11 run, after its fixes: 1165 (all pass, none skipped), SQL Server (LocalDB), PostgreSQL and Redis |
| Pattern counts | First counts in the Summary (Phase 11 run) |
| Hot-path budget | First run, by reading: about 10 round-trips cold; 0 within 5 s of the same URL; 4–5 after 5 s idle; 2 per 404; +3 per block list, +1 per single media picker. Phase 10 changed front-end caching since: re-measure |
| Package contents | Phase 11 run: all five packed; no stray files; Components carries the modal's script and CSS, Core no Radzen dependency or scoped CSS; dependencies at 4.1.3 |
| Template code | Phase 11 run: a site from the template, on project references, builds in Release with 0 warnings |
| Template smoke test | `tests/template-smoke.ps1` since Phase 11c, in CI's build job; passes |

## Coverage

- Phase 11 changed run (2026-11-27, 2f4dc41..773505c): the front-end path (content finders, not-found status,
  redirects and domains), bootstrap and start-up (validation, list binding, discovery, DI removals), the public
  surface (every type made internal, moved or removed, against its release notes), and the whole diff twice by
  reviewers given only the code (CI, `release.ps1`, `migrations.ps1`, the smoke test, packaging). Not done: product
  agents and the runtime probe R1 (no admin screen changed beyond a help text; the hot-path budget is not
  re-measured), API-break package validation, a real 4.x plugin on v5.

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
