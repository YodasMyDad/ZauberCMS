# ZauberCMS Audit Report

Last full run: 5668365 (2026-11-25, full) · Last changed run: 7442f14+wt (2026-11-28, Roadmap Phase 11h, 7442f14..working tree, merged after Phase 10g-2) · Mode: fix
Build: PASS · Warnings: 0 (warnings are errors since Phase 11b) · Vulnerable packages: 0 · Outdated majors: 15 (the .NET 11 set, held by D4)
Next free IDs: ZA-236 (findings) · ZP-004 (proposals) · Umbraco reference: not yet compared (first run with lenses G and H to come) · Parallel parts: ZA-220…ZA-229 unused (skipped, not to be filled); ZA-240…ZA-249 unused (Phase 10h-1's range; skipped, not to be filled); ZA-250…ZA-259 held by Phase 11i (in progress); ZA-260…ZA-264 used by Phase 10h-2, ZA-265…ZA-269 unused (skipped, not to be filled); ZA-270, ZA-271 used by Phase 10h-3, ZA-272…ZA-279 unused (skipped, not to be filled); ZA-280…ZA-289 held by Phase 10h-4 (in progress); ZP-010 used by Phase 10h-1, ZP-011…ZP-019 unused (skipped, not to be filled)
After Roadmap Phase 10h-3 (2026-11-28, merged after 10h-2): Build PASS · Warnings: 0 · Tests: 1510 (all pass, none skipped), SQL Server (LocalDB), PostgreSQL and Redis (1505 on its own branch) · Template smoke test passes; `doublecheck` self-review (with an independent review of the editors). The property editors, the installer and Core's generic messages localised (D120–D122); ZA-129 still in progress (closes with 10h-4); ZA-270, ZA-271 recorded (Low, Phase 10i); ZA-264 moved from Phase 12 to 10i (owner); ZA-272…ZA-279 unused (skipped, not to be filled). Next free ID: ZA-236
After Roadmap Phase 10h-2 (2026-11-28, on its own branch): Build PASS · Warnings: 0 · Vulnerable packages: 0 · Tests: 1505 (all pass, none skipped), SQL Server (LocalDB), PostgreSQL and Redis · `doublecheck` self-review on the part (three independent diff reviewers); verified in the browser. ZA-129 in progress (Content, Media, the editor dialogs and SEO converted, D110, D111; it closes with 10h-4); ZA-260 found and fixed (Low); ZA-261…ZA-263 recorded (Low, Phase 10i), ZA-264 (Low, Phase 12). ZA-265…ZA-269 unused (Phase 10h-2's range; skipped, not to be filled)
After Roadmap Phase 10h-1 (2026-11-28, merged after 10g-2 and 11h): Build PASS · Warnings: 0 · Vulnerable packages: 0 · Tests: 1500 (all pass, none skipped), SQL Server (LocalDB), PostgreSQL and Redis (1377 on its own branch) · Template smoke test passes; `doublecheck` self-review on the part. ZA-129 in progress (the mechanism and the first screens, D90–D97; it closes with 10h-4); ZP-010 proposed. Next free ID: ZA-236
After Roadmap Phase 11h (2026-11-28, merged after 10g-2): Build PASS · Warnings: 0 · Vulnerable packages: 0 · Tests: 1489 (all pass, none skipped), SQL Server (LocalDB), PostgreSQL and Redis (1400 on its own branch) · Template smoke test passes · `/audit changed` over the phase (a diff reviewer and a unit reviewer): ZA-191, ZA-192, ZA-195, ZA-196 fixed (D80–D83); ZA-230 found and fixed while building; ZA-231…ZA-234 found by the audit and fixed (Low); ZA-235 recorded (Low, Phase 12); ZP-002, ZP-003 proposed. Next free ID: ZA-236
After Roadmap Phase 10g-2 (2026-11-28): Build PASS · Warnings: 0 · Vulnerable packages: 0 · Tests: 1455 (all pass, none skipped), SQL Server (LocalDB), PostgreSQL and Redis · Template smoke test passes · ZP-001 built (D72); ZA-214 (D73), ZA-215 fixed; ZA-216 and ZA-197 found while building and fixed; ZA-217 recorded (Phase 12); `/audit changed` over the part (a diff reviewer and two unit reviewers): ZA-218 (Medium) found and fixed with the part's own findings, ZA-219 recorded (Low, Phase 12). Next free ID: ZA-220
After Roadmap Phase 10g (2026-11-28): Build PASS · Warnings: 0 · Vulnerable packages: 0 · Tests: 1366 (all pass, none skipped), SQL Server (LocalDB), PostgreSQL and Redis · Template smoke test passes · `/audit changed` over the phase (a diff reviewer and two unit reviewers): ZA-188 fixed (D38 phase, D66–D71); ZA-199…ZA-201 found and fixed while building; ZA-202…ZA-213 found by the audit and fixed (3 High, 4 Medium, 5 Low); ZA-214, ZA-215 recorded (Low, Phase 12); ZP-001 proposed. Next free ID: ZA-216
After Roadmap Phase 10f-2 (2026-11-27): Build PASS · Warnings: 0 · Vulnerable packages: 0 · Tests: 1221 (all pass, none skipped), SQL Server (LocalDB), PostgreSQL and Redis · ZA-187 (the URL lookup cached per domain and slug, D64), ZA-190 (domains matched below the path base everywhere, D64), ZA-189 (every redirect honoured, D65) fixed; Phase 10f done. Next free ID: ZA-199
After Roadmap Phase 10f-1 (2026-11-27): Build PASS · Warnings: 0 · Vulnerable packages: 0 · Tests: 1205 (all pass, none skipped), SQL Server (LocalDB), PostgreSQL and Redis · ZA-163 (after-commit hook `IAfterEntityCommit<T>`, D62), ZA-155 (resized copies removed with their image, D63), ZA-193 (a domain's redirects deleted through the change) fixed. Next free ID: ZA-199
After the Phase 11 audit follow-ups (2026-11-27): Build PASS · Warnings: 0 · Vulnerable packages: 0 · Tests: 1175 (all pass, none skipped), SQL Server (LocalDB), PostgreSQL and Redis · the owner's answers built: ZA-185 (antiforgery attributes on site controllers, D60) and ZA-198 (`Zauber:Jobs` switches, D61) fixed. Next free ID: ZA-199
After Roadmap Phase 11 (audit, 2026-11-27): Build PASS · Warnings: 0 · Vulnerable packages: 0 · Tests: 1165 (all pass, none skipped), SQL Server (LocalDB), PostgreSQL and Redis · `/audit changed` over the whole phase (three unit reviewers, two independent diff reviewers, two verifiers): nothing Critical or High from Phase 11; ZA-169…ZA-186 found and fixed (7 Medium, 11 Low), ZA-187…ZA-198 recorded (1 Medium, 11 Low, older than Phase 11). Next free ID: ZA-199. Earlier after-phase lines: [history.md](history.md) run log

> First full run. All findings come from reading code plus the deterministic checks below; **nothing was run in a
> browser or against PostgreSQL/Redis**, so items marked as needing a repro are reasoned, not observed. Critical and
> most High findings were re-read by the auditor before inclusion. No code was changed (report mode); five wrong
> claims in `AGENTS.md` / `Docs/` found by this run were corrected.

## Summary

| Severity | Open | New this run (Phase 11h and Phase 10g-2 changed runs) | Fixed this run (Phase 11h and Phase 10g-2, their audits included) |
| --- | --- | --- | --- |
| Critical | 0 | 0 | 0 |
| High | 0 | 0 | 0 |
| Medium | 0 | 1 (10g-2: ZA-218) | 1 (10g-2: ZA-218) |
| Low | 12 | 9 (11h: ZA-230…ZA-235; 10g-2: ZA-216, ZA-217, ZA-219) | 13 (11h: ZA-191, ZA-192, ZA-195, ZA-196, ZA-230…ZA-234; 10g-2: ZA-197, ZA-214…ZA-216) |

Proposals: three open, ZP-002 and ZP-003 (Phase 11h, owner decision) and ZP-010 (Phase 10h-1, owner decision);
ZP-001 built in Phase 10g-2 (D72).

Phase 10g-2 run: on added lines, no sync-over-async, fire-and-forget, markup, raw SQL, local time, regex or
suppression; the one `async void` is the modal's existing navigation handler, now guarded (ZA-197); three event
unsubscriptions added (ZA-197); seven `catch` blocks, each logs or says why it is empty.
Phase 10g run: no pattern count rose on an added line except two `MarkupString`s, now through the sanitiser (ZA-204),
and one `#pragma warning disable CS0618` with its reason (ZA-200).

Trend (first pattern counts, 2026-11-27, Phase 11 run): sync-over-async 48 · async-void 2 · fire-and-forget 7 ·
empty-catch 15 · unsanitised-markup 15 · raw-sql 1 · new-httpclient 0 · culture-unsafe-compare 6 (the suppressed EF
searches) · local-time 0 · regex-per-call 6 · count-gt-zero 0 · static-mutable 37 · event-subscribe 25 /
event-unsubscribe 22 · httpcontext-accessor 5 · exception-text 11. Flat against 2f4dc41 except regex 8 → 6 and
suppressed warnings 35 → 18; every hit on an added line is moved code or a suppression with its reason.

**Work order and owner decisions: [Docs/Roadmap.md](../Docs/Roadmap.md).** Every open finding is assigned there to
a phase (10h: ZA-129; 10i: ZA-128, ZA-194, ZA-261…ZA-264, ZA-270, ZA-271; 12: ZA-217, ZA-219, ZA-235); there is no Backlog (owner, 2026-11-27).
The run's two owner questions were answered and built in the Phase 11 audit follow-ups: ZA-185 (D60) and ZA-198 (D61).
ZA-151 was declined (D39). What each phase fixed and how it was
verified: [history.md](history.md); the earlier after-phase status lines are in its run log.

## Open findings

### ZA-128 [Low/High] Accessibility roll-up

- Where: `ZauberCMS.Components/Shared/TreeButton.razor:4`, `ZauberCMS.Components/Account/Pages/Login.razor:28-34` · Category: E · Since: 2026-11-25
- Issue / Fix: 8 of 38 icon buttons unlabelled; 7 clickable divs with no role or key handler (every tree button); ~139 Radzen inputs vs ~75 labels; `label for` without matching ids.

### ZA-129 [Low/High] Admin UI is not localised

- Where: `ZauberCMS.Components/` (166 razor files) · Category: E · Since: 2026-11-25
- Issue / Fix: 0 localiser uses, ~370 literal strings; `AdminDefaultLanguage` only affects formatting.
- Progress (Phase 10h-1, 2026-11-28): the mechanism is built (`IStringLocalizer<ZauberText>`, D90–D97) and the
  dashboards, shared components, modal, trees, context menus, data sources, section names and nav group headings
  are converted; 10h-2, 10h-3 and 10h-4 convert the rest (Roadmap "Phase 10h"; rules in Docs/AdminUI.md
  "Localisation"). Closes with 10h-4.
- Progress (Phase 10h-2, 2026-11-28): Content, Media, the editor dialogs and SEO, and Core's content, media,
  storage and SEO text (D110, D111).
- Progress (Phase 10h-3, 2026-11-28): the property editors (`Editors` but `Dialogs` and the API-key picker), the
  installer and Core's generic messages (D120–D122).

### ZA-194 [Low/High] With registration off, the not-found view is shown in the account layout
- Where: `ZauberCMS.Components/Account/Pages/Register.razor` · Category: E · Since: 2026-11-27 (older; visible since 11a sends the HTML) · Roadmap: 10i
- Issue / Fix: the not-found view renders inside the account layout, not its own. Render it as `EntryPage` does.

### ZA-217 [Low/High] A site with no `wwwroot` folder fails at start without saying why
- Where: `ZauberCMS.Core/Settings/OptionsFromZauberSettings.cs:46` (`ImageResizeOptionsFromZauberSettings`; was `ZauberSetup.cs:111` before Phase 11h moved it) · Category: F · Since: 2026-11-28 (older; found while building Phase 10g-2) · Roadmap: 12
- Issue / Fix: with no web root, `IWebHostEnvironment.WebRootPath` is null and `Path.Combine` throws
  `ArgumentNullException` (`path1`) when the options are validated at start. Fall back to the content root's
  `wwwroot` (or create it), or stop with a message that names the folder.

### ZA-219 [Low/High] Admin navigations to absolute addresses ignore a path base
- Where: `ZauberCMS.Components/Admin/UsersSection/UserEditor.razor` and `RoleEditor.razor` (sign-in refresh), the `Urls.*` constants passed to `NavigationManager.NavigateTo` across the admin · Category: E · Since: 2026-11-28 (older; found by the Phase 10g-2 audit) · Roadmap: 12
- Issue / Fix: under `UsePathBase` an address that starts with `/` leaves the path base, and the refresh's
  `redirectUrl` lacks it. Navigate to base-relative addresses (the current-user panel's were fixed in 10g-2), and
  pass `new Uri(NavigationManager.Uri).PathAndQuery` as the return address.

### ZA-235 [Low/High] An out-of-range `Zauber:ImageResize` value stops the start without naming the setting
- Where: `ZauberCMS.Core/Settings/ZauberSettingsValidator.cs` (`Validate`), ImageResize's options validator · Category: F · Since: 2026-11-28 (older; found by the Phase 11h audit) · Roadmap: 12
- Issue / Fix: a `DefaultQuality` outside 1–100, a `PngCompressionLevel` outside 0–9 or a negative cache or response
  number passes the Zauber check, then fails in the host's start-up validator from `app.Run()`, after the migrations,
  with ImageResize's message and no `Zauber:ImageResize:` path (D48). Run ImageResize's validator on the mapped
  options in `ZauberSettingsValidator` and name the setting.

### ZA-261 [Low/High] Two SEO inputs have no accessible name of their own
- Where: `ZauberCMS.Components/Seo/Dialogs/AddEditRedirect.razor:9` (the Domain list), `ZauberCMS.Components/Seo/SeoChecker.razor:9` (the address box) · Category: E · Since: 2026-11-28 (older; seen in the Phase 10h-2 browser check) · Roadmap: 10i
- Issue / Fix: the redirect panel's Domain drop-down is announced by the selected choice's id
  (`00000000-0000-0000-0000-000000000000` for "any domain"), and the page checker's address box has no label or
  placeholder. Give both a label from the Seo area (with ZA-128's pass).

### ZA-262 [Low/High] A list view's Delete moves the page to the recycle bin without asking
- Where: `ZauberCMS.Components/Admin/ContentSection/ContentListView.razor:49,76` · Category: G · Since: 2026-11-28 (older; seen in the Phase 10h-2 browser check) · Roadmap: 10i
- Issue / Fix: the row's Delete button recycles the page at once, while the tree's Delete asks first; Umbraco's list
  view asks before it moves items to the recycle bin. Ask with the tree's confirm, and say "Move to recycle bin".

### ZA-263 [Low/Medium] The SEO property's Open Graph image picker accepts any media, a folder too
- Where: `ZauberCMS.Components/Editors/SeoProperty.razor:57-59` (`MediaPickerSettings` with `AllowedMediaTypes` unset), `Editors/MediaPickerProperty.razor:111` · Category: G · Since: 2026-11-28 (older; seen once in the Phase 10h-2 browser check) · Roadmap: 10i
- Issue / Fix: choosing a folder in Choose Media set it as the page's Open Graph image. Limit the picker to images
  (`AllowedMediaTypes = [MediaType.Image]`), as Umbraco's image pickers do.

### ZA-264 [Low/High] The media image editor cannot be opened and writes to the console
- Where: `ZauberCMS.Components/Admin/MediaSection/Dialogs/EditImage.razor:265-266`, `Dialogs/UpdateMediaForm.razor:84-89` · Category: F · Since: 2026-11-28 (older; found by Phase 10h-2) · Roadmap: 10i (moved from 12, owner: every Phase 10/11 finding is done in those phases)
- Issue / Fix: the button that opens `EditImage` is commented out, so the component ships unused, and it writes its
  crop boxes with `Console.WriteLine`. Finish it or remove it, decided Umbraco's way: Umbraco never edits the stored
  file, crops are data (kept with the media, applied when the image is served).

### ZA-270 [Low/High] The trees' "Show more" row is not localised
- Where: `ZauberCMS.Core/Shared/Models/TreeLoadMore.cs:22` (`Name`), read by `ZauberCMS.Components/Core/Extensions/TreeExtensions.cs` (`TreeNodeText`, `AddLoadMore`) · Category: E · Since: 2026-11-28 (found by Phase 10h-3) · Roadmap: 10i
- Issue / Fix: "Show {n} more ({m} not shown)" is built in a Core model and rendered by static tree helpers, which
  have no localiser, so it stays English in every admin language. Give the row its text where the trees render it
  (a `shared_`/`trees_` key read with `L`, the counts as placeholders); the row is a tree button, already 10i's.

### ZA-271 [Low/High] Identity's own messages are English in the installer and the account pages
- Where: `ZauberCMS.Core/Installer/Services/InstallService.cs` (`error.Description` of the password and user validators), the account pages that show `IdentityResult` errors · Category: E · Since: 2026-11-28 (found by Phase 10h-3) · Roadmap: 10i
- Issue / Fix: a refused password or a taken user name shows ASP.NET Core Identity's English
  (`IdentityErrorDescriber`) whatever the language. Register an `IdentityErrorDescriber` that reads ZauberCMS's
  text (Core area Membership), as Umbraco localises its member and user validation messages.

## Improvement proposals

### ZP-002 [Low/Medium] A test that runs CI's commit-back step against a local origin
- Section: Developer experience · Lens: F · Since: 2026-11-28 (Phase 11h audit) · Needs: owner decision
- Today: `Packaging/BuildBaselineTests` ZA195 pins the text of the "Commit the stamped version back to main-static"
  step in `.github/workflows/ci.yml` (its order and commands). A reworded step that works fails the test, and a step
  that keeps the pinned text but breaks passes it; Phase 11h checked the behaviour by hand instead (the stamp and
  commit-back steps against a bare origin, 35 checks over 9 cases, again by its diff reviewer on Ubuntu 24.04).
- Proposal: a test that takes the step's script out of the workflow, runs it with `bash` and `git` against a
  scratch bare origin for those cases (a push in flight, a higher version, a person's change, a rollback, a lost push)
  and checks the result, skipped where `bash` is missing; the text pin then keeps only what the order needs.
- Why: the commit-back runs only on a push to `main-static`, which publishes to nuget.org, so a broken step is found
  after a release; the hand-run cases would be kept for every later change to the step.

### ZP-003 [Low/Medium] A site can replace the `login` and `register` rate-limit policies
- Section: Users and access · Lens: F · Since: 2026-11-28 (Phase 11h audit) · Needs: owner decision
- Today: `RateLimiterOptionsFromZauberSettings` (`ZauberCMS.Core/Settings/OptionsFromZauberSettings.cs`) adds the
  two policies, a sliding window per IP address from `Zauber:Identity:LoginRateLimit` / `RegisterRateLimit`. A site
  can change the numbers or turn a limit off, but not the limiter (partition key, algorithm, a store shared by the
  servers of a farm): adding its own policy of either name fails with `RateLimiterOptions`' duplicate-name error.
- Proposal: a replaceable default (AGENTS.md core principle), for example the two policies behind a public
  interface with the current ones as its default, or the policy names the account pages use as settings, so a site
  points them at a policy of its own. A public API and a release-notes entry either way.
- Why: the sign-in and registration limits are a security setting a site may need to fit its host (a CDN, a web
  farm, per-account limits) without forking ZauberCMS or giving up the built-in pages.

### ZP-010 [Low/Medium] Each back-office user chooses the admin's language
- Section: Users and access · Lens: H · Since: 2026-11-28 · Needs: owner decision
- Today: the admin runs in `Zauber:AdminDefaultLanguage` for everyone (D37, D96); the text mechanism (D90) follows
  the current UI culture, so it would serve a per-user language as it is.
- Proposal: a language on the user (`ExtendedData`, no migration), chosen on their page or in "Your account" (the
  current-user panel, ZP-001, built in 10g-2, D72), applied by `AdminCultureCircuitHandler` and `CultureMiddleware` in place of the setting when set.
- Why: Umbraco lets each back-office user pick their UI language from the installed language files
  (https://docs.umbraco.com/umbraco-cms/fundamentals/backoffice/users).

ZP-001 (every back-office user edits their own profile and password) was built in Phase 10g-2 (D72).

Lenses G (editor UX) and H (Umbraco alignment) have not had their first `full` run; the next one fills this section.

## Fixed this run / Resolved since last run

This run (Phase 11h changed run, 2026-11-28, merged after Phase 10g-2); details and how each was verified in
[history.md](history.md):
- Low: ZA-191 `IgnoreInternalRedirect` honoured (D80); ZA-192 settings set in code reach the bootstrap's options
  (D81); ZA-195 CI's commit-back with two pushes in flight (D82); ZA-196 blank providers name the setting (D83);
  ZA-230 detailed errors for static rendering; ZA-231 a later configuration source reported as code; ZA-232 an
  options comment; ZA-233 the commit-back after a rollback; ZA-234 the commit-back's messages.
- Proposals ZP-002 and ZP-003 recorded (owner decision).

Earlier this day (Phase 10g-2 changed run, 2026-11-28):
- Medium: ZA-218 the sign-in renewal after a change of the security stamp failed for a cookie over five minutes old,
  and the confirmation page renewed with the old stamp.
- Low: ZA-197 the vendored modal's subscriptions (moved from 10i); ZA-214 API keys stored and shown by value (D73);
  ZA-215 access checks outside the error boundaries, with the circuit's stamp check signing a tab out on a database
  error; ZA-216 `Zauber:ApiKeys` read by nothing (found while building).
- Proposal ZP-001 built (D72).

Earlier this day (Phase 10g changed run, 2026-11-28):
- High: ZA-202 deleting a role opened what it restricted; ZA-203 a user manager could take over a user with more
  sections; ZA-204 Editor Notes markup unsanitised.
- Medium: ZA-205 the D68 checks read stale sign-in roles; ZA-206 the starting role could open sections (D71); ZA-207
  role save left the restricted-media lookup cached; ZA-208 the role editor kept the previous role's tabs.
- Low: ZA-188 (the phase's own); ZA-199 the admin under a path base; ZA-200 the role editor could not delete; ZA-201
  login ignored its return address; ZA-209 `[Authorize]` on admin extensions; ZA-210 refused signed-in visitors sent
  to login; ZA-211 the return-address check; ZA-212 the change-password dialog; ZA-213 a routable dialog.

Earlier runs, kept until the next full run:

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
| Vulnerable packages | Phase 11h run: 0. Phase 10g run: 0, direct and transitive. Phase 11 run: 0. First run: 4 → ZA-102 |
| Outdated packages | Phase 11 run: HtmlSanitizer 11.0.26 → 11.0.74 and Radzen.Blazor 13.3.0 → 13.4.0 (no advisory); 15 majors, the .NET 11 set (D4). First run: see ZA-130 |
| Version alignment | OK — one `<Version>` 4.1.3 for the four packages, the template and its site match |
| Migration parity | OK — 5 migrations per provider, same names; Phase 11 changed only `public` → `internal` on them (no Up/Down change) |
| Config parity | Since Phase 11c the three `appsettings.json` files have the same `Zauber` keys and `Serilog` section (ZA-118, ZA-168; `AppSettingsParityTests`) |
| Docs drift | Paths all resolve; ~55 claims spot-checked, 5 wrong, corrected this run |
| Repo hygiene | Only `ZauberCMS.Web/app.db` tracked (exempt); `.gitattributes`, `.editorconfig`, `Directory.Build.props`, `Directory.Packages.props` and an SDK pin since Phase 11b (ZA-131 fixed) |
| Tests | Phase 11h run, after its fixes and the merge onto Phase 10g-2: 1489 (all pass, none skipped), SQL Server (LocalDB), PostgreSQL and Redis (1400 on its own branch). Phase 10g-2 run, after its fixes: 1455 (all pass, none skipped), SQL Server (LocalDB), PostgreSQL and Redis. Phase 10g run, after its fixes: 1366 (all pass, none skipped), SQL Server (LocalDB), PostgreSQL and Redis. Phase 11 run: 1165 |
| Pattern counts | First counts in the Summary (Phase 11 run) |
| Hot-path budget | First run, by reading: about 10 round-trips cold; 0 within 5 s of the same URL; 4–5 after 5 s idle; 2 per 404; +3 per block list, +1 per single media picker. Phase 10 changed front-end caching since: re-measure |
| Package contents | Phase 11 run: all five packed; no stray files; Components carries the modal's script and CSS, Core no Radzen dependency or scoped CSS; dependencies at 4.1.3 |
| Template code | Phase 11 run: a site from the template, on project references, builds in Release with 0 warnings |
| Template smoke test | `tests/template-smoke.ps1` since Phase 11c, in CI's build job; passes |

## Coverage

- Phase 11h changed run (2026-11-28, 7442f14..working tree, made alongside Phase 10g-2 and merged after it): the
  whole diff by a reviewer given the code (with the CI commit-back run against a local origin on Ubuntu 24.04), and
  bootstrap and settings end to end by a unit reviewer (ImageResize 3.5.0 probed: every part reads `IOptions`). At the
  merge, 10g-2's new email send was moved to `GetRequiredEmailProvider()` (the ZA-196 source scan) and the full suite
  run again. Not done: a product agent (no screen changed beyond the upload dialog's message), R1 (the default lookup
  path is unchanged), CI itself, a real Redis server with its address set in code; neither part's reviewers saw the
  other's code.

- Phase 10g-2 changed run (2026-11-28, 7442f14..working tree): the whole diff by a reviewer given the code and the
  requirement; Core (the account changes, confirmation, API keys, settings) and the UI (the panel, header, router,
  modal, editors, Global Settings, the user page) end to end by two unit reviewers. The orchestrator walked the screens
  in a browser on a scratch site while building, with a failing access service switched on and off (see the Roadmap's
  Phase 10g-2). Not done: a separate product agent and R1 (no front-end or caching change), a site under a real path
  base, Long Polling or Azure SignalR.

- Phase 10g changed run (2026-11-28, a749761..working tree): the whole diff by a reviewer given the code and the
  requirement; Core (the access service, policies, middlewares, seeder, membership changes) and the UI (router, entry
  page, account pages, layout, Users and Settings screens) end to end by two unit reviewers. The orchestrator walked
  the screens in a browser on a scratch site while building (see the Roadmap's Phase 10g). Not done: a separate
  product agent and R1 (no front-end or caching change beyond the role-cache), pattern counts over the whole tree
  (added lines only), a site under a real path base, two servers.

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
