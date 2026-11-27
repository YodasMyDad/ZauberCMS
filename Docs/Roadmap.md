# Rework 2026 — Roadmap & Handoff

**Start here when resuming the rework.** Read order: [AGENTS.md](../AGENTS.md) → this file →
[.audit/report.md](../.audit/report.md) (findings by ID) → the Docs file for the area you're touching.

## Status (2026-11-27)

- Branch **`rework-2026`** (pushed). All rework happens here. **Do not merge or push to `main-static`** until the
  owner says so — every push there publishes to nuget.org.
- Done: docs consolidated and fact-checked; first full audit run (IDs now `ZA-001`…`ZA-161`).
- **Phase 0 done** (ZA-044), **Phase 1 done** (ZA-102, ZA-130, ZA-133: all packages on their latest .NET 10
  versions), **Phase 2 done** (test projects + CI test step), **Phase 3 done** (broken admin actions) and **Phase 4 done**
  (access control & identity). After Phase 4 the owner asked for three follow-ups, also done (see "After Phase 4" below).
  **Phase 5 done** (content integrity). **Phase 6 done** (PostgreSQL & Redis). **Phase 7 done** (installer,
  2026-11-26). **Phase 8 done** (media recycle bin, 2026-11-26). **Phase 9 done** (Block List redesign, 2026-11-26).
  **Phase 10 done** (performance & async, 2026-11-26).
  **Phase 10a done** (callers outside a circuit, D27, 2026-11-26).
  **Phase 10b done** (save hooks and content data, D28–D29, 2026-11-26).
  **Phase 10c done** (property editors and stored values, D30–D31, 2026-11-26).
  **Phase 10d done** (admin UI robustness and the resizable tree panel, D32, 2026-11-27).
  **Phase 10e done** (front end, security and identity hardening, D33–D36, 2026-11-27).
  **Phase 11a done** (the not-found page and redirect domains, D41–D42, 2026-11-27).
  **Phase 11b done** (engineering baseline, CI and release hardening, D43–D44, 2026-11-27).
  **Phase 11c done** (packaging and the template, smoke test in CI, D45–D47, 2026-11-27).
  **Phase 11d done** (start-up checks, settings binding and DI leftovers, D48–D50, 2026-11-27).
  **Phase 11e done** (the MVC path removed, UI contracts out of Core, D51–D52, 2026-11-27).
  **Phase 11f done** (public surface and API conventions, D53–D54, 2026-11-27).
  **Phase 11g done** (content finders at the owner's request, D55; the service split, `Content.Url` and the custom
  components' page id, D56–D58; the DbContext pattern kept, D59; 2026-11-27). **Phase 11 is done** (parts 11a–11g)
  **and audited** (`/audit changed`, 2026-11-27: ZA-169…ZA-186 fixed, ZA-187…ZA-198 recorded; see "Phase 11 audit").
  **Phase 11 audit follow-ups done** (the owner's answers: antiforgery attributes on site controllers, D60, ZA-185;
  `Zauber:Jobs` switches, D61, ZA-198; 2026-11-27).
  **Phase 10f-1 done** (after-commit hook `IAfterEntityCommit<T>`, D62, ZA-163; resized copies removed with their
  image, D63, ZA-155; a domain's redirects deleted through the change, ZA-193; 2026-11-27).
  **Phase 10f-2 done** (the URL lookup cached per domain and slug, and domains matched below the path base on both
  sides, D64, ZA-187, ZA-190; every redirect honoured, D65, ZA-189; 2026-11-27). **Phase 10f is done.**
  **9 open findings** (0 Critical, 0 High, 0 Medium, 9 Low), each one assigned below to a phase (10g, 10h, 10i, 12),
  plus back-office roles (D38). **There is no Backlog** (owner, 2026-11-27): every open item is in a phase,
  and a new finding goes into the phase whose theme fits. **Next: Phase 10g** (back-office roles, D38, with ZA-188),
  then 10h, 10i, then Phase 12 (design questions inside a phase are decided Umbraco's way and recorded, as the owner
  delegated).
- Findings come from reading code + a real build/package scan; items marked "needs repro" must be reproduced before
  or while fixing. Since Phase 6 the tests run on PostgreSQL and Redis too (Docker).
- Tests: `dotnet test --solution ZauberCMS.sln -c Release` (see [Standards.md](Standards.md) §9).

## Owner decisions (2026-11-25)

| # | Decision | Affects |
| --- | --- | --- |
| D1 | **PostgreSQL and Redis caching are supported and must work.** Treat their defects as real bugs, not edge cases. | ZA-050, ZA-007, ZA-051, ZA-014, ZA-055 |
| D2 | **Replace "first registered user becomes admin" with an explicit setup step — a polished installer.** | ZA-004 (also ZA-023, ZA-024) |
| D3 | **Media gets a recycle bin like content:** delete → recycle bin (restorable); deleting *from* the bin is permanent (row **and** file removed). A direct permanent-delete option is also allowed. | ZA-006, ZA-053 |
| D4 | **Dependencies: first bring everything (Radzen included) to the latest versions that run on .NET 10, then test the app.** Only once it all works well: move to .NET 11 and test again. | ZA-102, ZA-130 |
| D5 | Block List editor is to be **simplified/redesigned**, not patched indefinitely. | ZA-034…ZA-043, [BlockListEditor.md](BlockListEditor.md) §6 |
| D6 | **SVG uploads are sanitised on upload** (scripts, event handlers, external references removed) and served with a restrictive Content-Security-Policy. | ZA-005 |
| D7 | **Sanitiser: `<iframe>` only from a configurable host allow-list** (default YouTube, Vimeo); `data:` URLs only for images. | ZA-021, ZA-134 |
| D8 | External sign-up (ZA-002): fix with unit tests now; the owner tests against a real provider later. Until then it is "not verified live". | ZA-002 |
| D9 | Fix the duplicated `<RadzenComponents>` (ZA-138) in Phase 4. | ZA-138 |
| D10 | *Replaced by D27 (2026-11-26).* **Service authorisation is an opt-in permission hook, done as its own later phase** (Phase 10a): services ask a replaceable permission service before a write; the default allows everything, a site can switch on a strict mode. A strict default is a question for the major version. | ZA-140 |
| D11 | **Redirects may point at another site** (a full http/https address). | ZA-116 area |
| D12 | **ZauberCMS.RTE 4 is taken now on .NET 10** (it targets net10.0 and net11.0), not at the .NET 11 move. | ZA-134, ZA-136 |
| D13 | **Cache: entity graphs stay in each server's memory cache; Redis only carries invalidation** (pub/sub "clear prefix" plus a version counter, so every server drops stale entries). Nothing secret or half-loaded is stored in Redis; no public API change. Shared DTO caching is not planned. (2026-11-25) | ZA-051, ZA-014, ZA-055, ZA-072 |
| D14 | **PostgreSQL Initial migration:** reproduce on a real PostgreSQL first; if it fails, correct the PostgreSQL `Initial` migration (one-off exception to rule 1, explained in the commit and `Docs/Data.md`), then add the missing ContentVersion indexes for all three providers through `migrations.ps1`, clearing duplicate "current published" flags before the unique index. (2026-11-25) | ZA-050 |
| D15 | **Tests run against real PostgreSQL and Redis containers locally and in CI** on every push; skipped with a clear message when Docker isn't available. SQL Server: the same test classes run against a SQL Server the developer points `ZAUBER_TEST_SQLSERVER` at (not on CI yet). (2026-11-25) | Phase 6 |
| D16 | **ZA-143 goes at the end of Phase 6**, after the PostgreSQL/Redis work, both halves together. (2026-11-25) | ZA-143 |
| D17 | **No hotfix for ZA-044.** The fix ships with the v5 release when the owner is ready; 4.1.3 stays as published until then. (2026-11-25) | ZA-044 |
| D18 | **The whole rework ships as one v5 major, together with .NET 11; there are no 4.x releases before it** (owner, 2026-11-25, replacing "4.x minors with shims"). A breaking change is allowed when it buys something, and each one is listed in that phase's release notes so the v5 upgrade notes can be built from them. Keep the `[Obsolete]` path where it costs little. | Phase 11, 12 |
| D19 | **Installer works like Umbraco's (large scope):** database (SQLite / SQL Server / PostgreSQL, connection test) → admin account → site name and default language → optional starter content → summary. It writes `Zauber:DatabaseProvider` / `ConnectionString` into `appsettings.json` and restarts. No unattended install. (2026-11-25) *As built (2026-11-26): the site switches to the new database without restarting; see Phase 7.* | ZA-004, Phase 7 |
| D20 | **The installer is open to the first visitor while the site is not installed** (as in Umbraco and WordPress), no setup code. Install the site before making it public; say so in the docs. (2026-11-25) | ZA-004 |
| D21 | **"Installed" is a flag stored in the database**, set by the installer. On upgrade, a site with any user is marked installed automatically. The installer never reopens, even with no admin left; recovery is a documented config key that promotes one existing user on the next start. (2026-11-25) | ZA-004 |
| D22 | **`AdminEmailAddresses` promotes only after the email is confirmed.** (2026-11-25) | ZA-023 |
| D23 | **Public registration switch:** existing sites default ON (no change on upgrade), the installer asks (default OFF for new sites); off = page 404 and link hidden. When on: built-in honeypot + minimum fill time, its own rate limit, and a discovered extension interface for CAPTCHA providers (Turnstile, reCAPTCHA, hCaptcha) from the site's project. (2026-11-25) | ZA-024 |
| D24 | **No switch to turn the installer off.** Empty database settings always mean "show the installer", as decided in D20. That a live site which loses its database settings (a publish overwriting `appsettings.json`, a missing environment variable) then shows the installer is accepted: it is logged as an error at start-up and documented, with the instruction to put the settings into the project or the environment after installing on a server. (owner, 2026-11-26, asked during Phase 7 after the audit raised it) | ZA-004, [Installer.md](Installer.md) |
| D25 | **Content versions are pruned: the newest N per page are kept** (plus its published version and latest draft), by the daily job; a new setting `Zauber:ContentVersionsToKeep`, on by default (50) — existing sites start pruning on upgrade (release notes). (owner, 2026-11-26, Phase 10) | ZA-069 |
| D26 | **Admin trees page their children with a "Show more" row**: the first 100 of a node, then a row at the bottom that loads the next 100. (owner, 2026-11-26, Phase 10) | ZA-068 |
| D27 | **Services do no authorisation; access is checked where a call comes in** (the page, component or endpoint), as in Umbraco and WordPress. Replaces D10: the opt-in permission hook was weighed in Phase 10a and dropped (no shipped path needs it, it does nothing against plugin code, and it would put a rule on every write method). What Phase 10a does instead: `ICurrentUserAccessor`, so services can be called outside a circuit (ZA-093). Back-office roles other than Admin: Phase 10g (D38, section access per role); service checks would be looked at again after that. (owner, 2026-11-26) | ZA-140, ZA-093 |
| D28 | **Save hooks see every row a change writes, and one veto stops the whole change.** `SaveChangesAndLog` runs `IBeforeEntitySave<T>` for every row the save writes, of every entity type (the change tracker), each with its own state, then saves once, then runs `IAfterEntitySave<T>` for the same rows. A move, recycle, restore, permanent delete or emptied bin is one change: any `false` from a before-save hook and nothing of it is written (the transaction rolls back). Umbraco does the same: cancelling `Moving`, `Deleting` or `EmptyingRecycleBin` cancels the whole operation, and the "-ed" notifications list every item moved or deleted. Our hooks are per row rather than per operation, so they also see the items below (Umbraco's "-ing" notifications list only the item named). Not hooked: bulk maintenance that runs straight against the database (audit and version retention, orphan clean-up). (owner delegated to Umbraco parity, 2026-11-26, Phase 10b) | ZA-012, ZA-157, ZA-159, ZA-018 |
| D29 | **A "Save only" draft runs the hooks of the rows it writes, like any save: the draft rows, not the live page.** It writes `UnpublishedContent` (the page's and its blocks' drafts), so `IBeforeEntitySave<UnpublishedContent>` runs; the live `Content` row only gets its link to the draft (Modified), and a new block's row, which only the draft reaches, is Added. A plugin that mirrors the live site (a search index) watches `Content`; one that wants drafts too watches `UnpublishedContent`. Umbraco separates the same way: Save raises `Saving`/`Saved` and Publish raises `Publishing`/`Published` as well; its external index holds published content only. Tag links follow the live values, as Umbraco's tags do: a draft leaves them, a publish, version restore or copy writes them, a permanent delete removes them. (owner delegated to Umbraco parity, 2026-11-26, Phase 10b) | ZA-159, ZA-099 |
| D30 | **A picker stores one shape for any count: a JSON array of ids in the order the editor chose, even for one item; older values are read, not rewritten.** The content and media pickers wrote one item as a bare id and several as a JSON array. They now always write `["id", …]`, and everything that reads a picker value (`ToPickerIds`, `GetContentItems`, `GetMediaItems`, `GetUsers`, `GetValue<Guid>` and `GetValue<List<Guid>>`, the admin's single-id fields) takes both shapes. There is no data migration: values live in pages, drafts, versions and block snapshots across three providers, and a value becomes the new shape the next time its page is saved. Umbraco does the same: Media Picker 3 stores a JSON array whatever the maximum, and its value converters return one item or a list from the property's settings, not from the stored shape; when a stored format changed, its converters kept reading the old one. The read side returns the stored order, as Umbraco's converters do. (owner delegated to Umbraco parity, 2026-11-26, Phase 10c) | ZA-029, ZA-078 |
| D31 | **Stored values do not depend on the culture: numbers and dates are written in the invariant culture, and property aliases are unique on a page.** The numeric editor writes `1.5` and the date editor `2026-11-26T14:30:00` (with the time, which the long-date text dropped); values written in the admin's culture before this still read, the invariant form first. A content type is refused when two of its properties share an alias, ignoring case, on the type itself or through a composition (from either side); values that already repeat an alias read the first instead of throwing. Umbraco stores decimals and dates invariant, and refuses a document type or composition whose aliases clash. (owner delegated to Umbraco parity, 2026-11-26, Phase 10c) | ZA-027, ZA-028 |
| D32 | **An error in one part of the admin is shown in that part, with "Try again"; the circuit and the rest of the page stay.** `AdminErrorBoundary` sits round each property editor, each dashboard tab, the sidebar navigation and the page body (cleared on navigation); each side panel's content has one too, with "Close", and so do Radzen's dialogs (round `<RadzenComponents />`). It says what could not be shown, logs the exception, and shows the exception's message only when Blazor's detailed errors are on (`CircuitOptions.DetailedErrors`), otherwise "The details are in the site's log". An editor that failed keeps its last value, so a save does not lose it. Umbraco does the same: a property editor or extension that fails shows an error in its place and the rest of the document stays editable, and server errors show their details to the back office only in debug mode. Retry is offered because a failure is often passing (a timeout, a missing service a reload brings back); Umbraco's answer is a reload, which here would lose unsaved work. (owner delegated to Umbraco parity, 2026-11-27, Phase 10d) | ZA-097 |
| D33 | **Front-end errors get an error page outside Development: the site's own page at `Zauber:ErrorPagePath` (default `/error`), else a plain built-in page.** Setup adds ASP.NET's exception handler for that address, so editors can make the error page a content page at `/error`, or a site can point the setting at a page of its own; when there is no page there, or it fails as well, a built-in page (no database, no layout, the request id) is sent with status 500. Empty turns it off for a site that sets up its own handler. Umbraco also relies on ASP.NET's exception handler and lets the site provide the page. (owner, 2026-11-27, Phase 10e) | ZA-098 |
| D34 | **A redirect says whether its From URL is a regular expression (`IsRegex`, a nullable column); otherwise it matches exactly.** Rows saved before the column existed are null and keep the old guess (regex characters mean regex) until they are saved again, so no site changes behaviour on upgrade. A save is refused when the redirect would send an address to itself or close a circle with the saved ones, and the middleware never redirects an address to itself. (owner, 2026-11-27, Phase 10e) | ZA-116 |
| D35 | **Deleting a user erases the person; there is no user recycle bin.** The row stays, marked deleted, so pages and versions still point at an author, but its user name becomes `deleted-<id>` and its email, phone, password, profile values, logins, claims and roles go; the address and the old name can register again. Audit rows keep their text until the daily job removes them, as the record of who did what. Umbraco also deletes members for good. Users deleted before v5 are left as they are, with no erase on upgrade. (owner, 2026-11-27, Phase 10e) | ZA-033 |
| D36 | **Image resizing does not upscale, and the resize cache is capped at 2 GB, by default.** `Zauber:ImageResize:AllowUpscale` false (a bigger size than the original returns the original size) and `Cache:MaxCacheBytes` 2147483648 (the oldest copies are removed as new ones are written). Both can be set back; existing sites notice, so it is in the release notes. (owner, 2026-11-27, Phase 10e) | ZA-104 |
| D37 | **The admin's text is localisable; ZauberCMS ships English.** Every string the admin shows goes through .NET localisation (`IStringLocalizer`) with English resource files, so a site or a translator adds a language with resource files of its own, without forking. The admin's language stays `Zauber:AdminDefaultLanguage`. Front-end views are the site's own and are not part of it. Umbraco ships language files the same way. (owner, 2026-11-27) | ZA-129 |
| D38 | **Back-office access is set per section and per role.** An admin screen sets which roles may open each section (Content, Media, Users, Settings, and plugin sections, which take part through `ISection`); Admin always sees everything, and a seeded "Editor" role gets Content and Media. The navigation, the pages under `/admin` and the endpoints the admin calls are all gated. Start nodes and per-action permissions (publish, delete, move) are not part of it; this is the first step of Umbraco's user groups. Services still do no authorisation (D27). (owner, 2026-11-27) | D27 |
| D39 | **The installer's pages stay fixed; a site cannot restyle them or add steps.** `IInstallService` stays replaceable. ZA-151 is declined. (owner, 2026-11-27) | ZA-151 |
| D40 | **The installer keeps writing the database settings to `appsettings.json` (D19 stands).** A separate file would be lost on deployments that delete extra files anyway; the docs keep telling people to put the settings in their deployed configuration or environment variables. (owner, 2026-11-27) | D19, D24 |
| D41 | **An address with no page is answered by ZauberCMS with the site's `Zauber:NotFoundComponent`, in its layout, with status 404 set as the response starts.** .NET 10 sends a static render that ends at 404 without its HTML, so that status-code pages can re-execute the request; Blazor's own answer is a Router `NotFoundPage` (a routable component of a fixed type) or status-code re-execution to an address. Both were set aside: the not-found view is a site's setting chosen at run time with its own layout and the request's content and culture, and re-execution would route a second request. `EntryPage` sets the 404, so a site's view needs no code for it; `HttpContext.SetStatusCodeWhenResponseStarts` (Core, public) is for a site page that shows a not-found view of its own, and changes only a response still at 200, so a redirect or the error page (D33) keeps its status. Umbraco also renders its configured 404 page (the last-chance content finder / `Error404Collection`) with status 404 in the same request, without re-execution. (owner delegated to Umbraco parity, 2026-11-27, Phase 11a) | ZA-164 |
| D42 | **A redirect with a domain applies only on that domain; one without applies on every host; for the same address the one for the request's domain wins.** The domain is matched by the rule content routing uses (same host; a domain's path, as in `example.com/fr`, on whole segments), and the From URL stays the whole path. Exact rules are still tried before regular expressions, and within each the ones for a domain come first. The loop check counts only rules that can fire on the same requests. Umbraco's redirect URL management keeps each redirect with a content item's culture and domain (its root), so a redirect is never global; ZauberCMS keeps "any domain" because a single-site install has no domains and its redirects must keep working. (owner delegated to Umbraco parity, 2026-11-27, Phase 11a) | ZA-165, ZA-166 |
| D43 | **The .NET analysers run at the .NET 10 rule set with the Recommended rules of five categories as warnings, and warnings are errors.** `AnalysisLevel` is pinned to 10.0 (a newer SDK adds no warnings by itself; Phase 12 moves it to 11.0) and `AnalysisMode` is Recommended for Globalization, Performance, Reliability, Security and Usage; Design and Naming stay at the default, because their rules rename or reshape public API, which the v5 clean-up (11f) settles. Every project has `TreatWarningsAsErrors`, except for vulnerability advisories (NU1901–NU1904): a new advisory is not caused by the change being built, so it stays a warning locally and CI's scan fails on it. Rules turned down are in `.editorconfig`, each with its reason (logging-delegate rules CA1848/CA1873 are suggestions; two test-only rules are off; migrations count as generated code). The code-style analysers (`EnforceCodeStyleInBuild`) are not on: the style rules are IDE suggestions, so no file is reformatted for them. Recommended for every category gave about 740 hits, most of them test-method names (CA1707), log calls and public names. Umbraco's repository has the same pieces (`Directory.Build.props`, central package management, `.editorconfig`, an SDK pin that rolls forward to the latest feature band) and works down a list of warnings; ZauberCMS starts at zero, so it keeps zero by making them errors. (owner delegated, 2026-11-27, Phase 11b) | ZA-131 |
| D44 | **One version is built from one commit, and its tag is pushed before any package.** The publish job reserves the version by pushing its tag `vX.Y.Z` first, then builds and pushes the five packages in dependency order (Core, Components, Routing, ZauberCMS, Template). A later failure never lets the next commit reuse the version: that commit finds the tag and gets the next one; a rerun of the same commit finds its tag at HEAD and publishes the same code again, skipping the packages already pushed (a first run fails on a package version that exists). The version: `<Version>` in `Directory.Build.props` is published as written when it is above the latest stable tag; at or below it, the patch of that tag is counted up (as before); a prerelease (`X.Y.Z-label`) is published once, as written, and must be bumped before the next push. Commits that change only documentation do not run CI, so they do not publish. Umbraco publishes from a release tag a release manager creates, its version from the repository (Nerdbank.GitVersioning); ZauberCMS keeps publishing on a push to `main-static`, with the tag taken first so that a tag always names the one commit its packages came from. (owner delegated, 2026-11-27, Phase 11b) | ZA-089, ZA-109 |
| D45 | **The packages carry the repository's `ReadMe.md` as their readme, the ZauberCMS logo as their icon, Source Link and a symbol package; each lists only the packages its project references do not bring.** One readme for all five packages (it already was the template's), so there is one text to keep right and it is the one GitHub shows; the icon is the logo the template package already had, no new artwork. A readme is fixed per package version on nuget.org, so a commit that changes only `ReadMe.md` still runs no CI and publishes nothing (`paths-ignore` keeps it): the next release carries the text, and no version is spent on something no site runs. The PDBs go into `.snupkg` symbol packages for nuget.org's symbol server, with Source Link (part of the .NET SDK) pointing at the commit on GitHub and untracked (generated) sources embedded; CI builds set `ContinuousIntegrationBuild` so the PDBs hold no build-machine paths, local builds keep local paths for the debugger. Dependencies: Components, Routing and ZauberCMS stop repeating what Core brings; a site resolves the same packages at the same versions (central package management), and a package comes into the graph in one place. Umbraco's `Directory.Build.props` sets the icon, Source Link (`PublishRepositoryUrl`) and `snupkg` symbol packages for all its packages the same way; ZauberCMS adds the readme, because its nuget.org pages had no description beyond one line. (owner delegated, 2026-11-27, Phase 11c) | ZA-119 |
| D46 | **The template smoke test is a script in the repository (`tests/template-smoke.ps1`) that CI runs in the build job on every push, so a template or package that does not make a working site is never published.** It builds the packages at a throw-away version into a local feed, installs the template in a private template-engine hive, makes a site with `dotnet new zaubercms`, publishes and starts it, goes through the installer over HTTP and checks the pages, `blazor.web.js` and every admin script and style. It is not an xUnit test: it needs nuget.org and builds the packages, and the test suite has no network (Standards §9). PowerShell, like `release.ps1` and `migrations.ps1`, and written for Windows PowerShell 5.1 and PowerShell 7 (CI's runner). On CI it adds the time of a second package build and a site restore (the whole script took 54 seconds in a Linux SDK container with an empty NuGet cache). Umbraco's pipeline also makes a site from the template package it has just built and runs its acceptance tests against it. (owner delegated, 2026-11-27, Phase 11c) | ZA-094, ZA-123, Phase 2 |
| D47 | **A site made from the template keeps out of git what it writes while it runs: the SQLite file, uploaded media, the resize cache and the logs, besides build output; its launch profiles are `https` and `http`.** The database and media of a live site are backed up with the server, not committed, and a cloned site starts at the installer; a site that wants its media in git removes the line. Profiles are named as in `dotnet new blazor` (they were "Umbraco.Web.UI"). Umbraco's template `.gitignore` leaves out the same: `umbraco/Data` (its SQLite database and temp files), `umbraco/Logs` and `wwwroot/media`. (owner delegated, 2026-11-27, Phase 11c) | ZA-123 |
| D48 | **A `Zauber` setting whose wrong value would fail at the first request that needs it stops the site at start-up, naming the setting, the value and what would be valid; one whose wrong value only does nothing is a warning at every start.** Failing: a `Zauber:Plugins:*` provider name that discovery does not find (it threw `KeyNotFoundException` when media or email was first used; a blank one is still "no provider") and an `AdminDefaultLanguage` that is not a culture (it already failed at start-up, without the setting's name). Warnings: a `SiteUrl` that is not an http(s) address (ignored, so email links come from the Host header), a `NotFoundComponent`/`StarterComponent`/`MissingView`/`Identity:AccountLayout` that is not found (nothing is shown); these do not fail because a site running today with one of them must still start after the update. `IValidateOptions<ZauberSettings>` with `ValidateOnStart`, and `AddZauberCms(app)` reads the settings first, so the error comes before anything is served; the shipped defaults, the three `appsettings.json` files and empty database settings (D24) pass with no warning. What the bootstrap finds before logging exists (types that cannot load, a bootstrap extension without a parameterless constructor, a non-public extension type) is collected and logged by `AddZauberCms(app)` (category `ZauberCMS.Startup`). Umbraco validates its settings the same way, with `IValidateOptions` validators for its settings classes, so a bad value stops the boot with an `OptionsValidationException`; its type finder logs the types it cannot load and goes on. (owner delegated, 2026-11-27, Phase 11d) | ZA-120, D24 |
| D49 | **A list in configuration replaces the setting's default list; without the key the default stays.** The configuration binder adds what it reads to a list that already has items, so `ImageResize:ContentRoots` had each shipped root twice and a site could never drop a default. After binding, `ZauberSettingsBinding` sets every list the section names to the configured list (dictionaries stay merged, as their keys are meant to add up); a test fails for any list setting with a default that is not replaced. `new ZauberSettings()` keeps its defaults, so no code that reads them changes. An empty JSON list (`[]`) keeps the default (it reaches the binder as no list); to resize no images, set `ImageResize:EnableMiddleware` to false. ASP.NET's options pattern has no switch for this (the binder appending is by design); Umbraco's settings classes also start collections from defaults and bind over them, so there a site's values are added to its defaults. (owner delegated, 2026-11-27, Phase 11d) | ZA-167 |
| D50 | **DI registrations nothing in ZauberCMS uses are removed; public types and members that are left over are `[Obsolete]` for removal in a later major.** Removed: the scoped `HttpClient` (its base address was the request's own, from the Host header, and it failed outside a request; an injected `HttpClient` is now the framework's default client from `AddHttpClient`), the `IUserEmailStore<User>` registration (`UserManager` uses the Entity Framework store, which is its email store) and the second `SignInManager<User>` registration (`AddSignInManager<ZauberSignInManager>()`). Obsolete: `IdentityUserAccessor` (still registered, so a site that injects it keeps working; `ICurrentUserAccessor` replaces it, D27), `UserEmailStore`, `ContentNavGroup.Actions` (never read). Umbraco registers no bare `HttpClient` for sites either; it uses `IHttpClientFactory`. (owner delegated, 2026-11-27, Phase 11d) | ZA-121, D18 |
| D51 | **ZauberCMS.Core has no UI library: a contract that takes a Radzen or modal type lives in ZauberCMS.Components, under the namespace it had in Core.** Moved: `IContentProperty` (its `ModalService` is the modal's `IModalService`), `ISectionNavGroupAction` (Radzen's `MenuItemEventArgs`/`ContextMenuService`), the extensions `DialogExtensions`, `NotificationServiceExtensions`, `TreeExtensions` and `PropertyExtensions.GetPropertySettings(this IContentProperty)` (now `ContentPropertyExtensions`), the services `ValidateService<T>` and `MediaValidationService` (Radzen notifications), and the vendored Blazored.Modal source (`Blazored.Modal*` namespaces kept). The folders `ZauberCMS.Components/Core/` and `Modal/` hold them. Their services are registered by an internal start-up plugin in Components (`AdminUiStartup`), found by the bootstrap like any `IStartupPlugin`, so a site calls nothing new. Core drops its Radzen package; it keeps Razor component support for the installer's pages and ZauberCMS.RTE for rendering rich text. Keeping the namespaces means a site's or plugin's source keeps compiling once it references Components (the `ZauberCMS` package does); it is a binary break (the assembly changed), which v5 allows (D18). No implementation moved or was renamed, so no stored type name changes (rule 2). Umbraco does the same: Umbraco.Core holds no back-office UI types (property editor UIs and dashboards are in the back-office packages), and a package with back-office UI references those. Splitting `IContentProperty` into a Core half and a UI half was not done: Core has no use for an editor without its UI, and two interfaces would break every editor's source instead of none. (owner delegated, 2026-11-27, Phase 11e) | ZA-091, D18 |
| D52 | **ZauberCMS renders pages only through Blazor (`EntryPage`); the never-mapped MVC rendering path is removed, not made obsolete.** Removed: `IContentFinder`, `ContentFinderPipeline`, `DefaultContentFinder`, `ZauberRouteValueTransformer`, `ZauberCMS.Core.Rendering` (`ZauberViewPage<T>`, `IZauberViewPage`, `ZauberPageViewModel`), `HtmlHelperExtensions`, `RenderExtensions`, Routing's `Views/` (the `ZauberRender` views, the `RenderBlocks` and `BootstrapPager` view components) and `IContentBlockView.RenderMode` (an MVC enum only that path read). Nothing ever ran them: the routes were commented out, `IContentFinder` was never in the discovery list or the docs as an extension point, so a site that implemented it had code that never ran. `[Obsolete]` would have kept MVC types in Core's public contracts for the whole v5 line for no working feature. `AddControllersWithViews` became `AddControllers` (only `AuthController` is MVC), so what the view services brought and live code uses is registered by itself: `IMemoryCache` and a memory `IDistributedCache` (Redis's stays when configured); the sign-out POST checks its antiforgery token itself (`[ValidateAntiForgeryToken]` needs the view services and failed with 500 without them). A site with its own MVC views calls `AddControllersWithViews()`. Umbraco finds pages with a content-finder pipeline (`IContentFinder`, `IContentLastChanceFinder`) that sites extend; ZauberCMS has none working, and adding one is a feature for the owner to decide (Phase 11e question). (owner delegated, 2026-11-27, Phase 11e) | ZA-090, D18 |
| D53 | **A type is public only when a site or plugin needs it; what is public is pinned by a test.** Public: discovery interfaces and every type discovery finds (the built-in ones too: discovery sees exported types only), every type a database or setting stores by full name, service interfaces with their default implementations and the types `AddZauberCms` registers for a site to resolve, replace or decorate, parameters, models, results, enums, settings, the DbContexts, extension methods (the `MapTo` copiers included: the admin's `ContentEditor` uses one), Razor components, anything in a public signature, and any type another ZauberCMS package uses. Internal: EF entity mappings, migrations (`migrations.ps1` rewrites EF's `public` on a new one) and the design-time factories, the middleware `AddZauberCms` wires, hosted services, circuit handlers and start-up plugins ZauberCMS adds for itself, endpoint mapping the bootstrap calls, and plumbing (`AssemblyManager`, its unused public mutable caches removed). Leftover public types nobody uses are `[Obsolete]` (D50), not removed. `InternalsVisibleTo` only for the test projects: the packages use each other through public API, so a site that updates one package never meets a missing internal of another (so `CultureMiddleware` stays public, EntryPage calls its `GetEntryModel`). `PublicApiTests` pins the public types in `Snapshots/PublicTypes.txt`; the rule is in [Standards.md](Standards.md) §1. Umbraco keeps its service implementations, notification handlers and composers public so sites subclass, decorate and replace them, and keeps infrastructure (EF/NPoco mappings, migrations plumbing, middleware internals) out of its supported API; ZauberCMS does the same: extending and updating win over a smaller surface (core principle). Core went from 429 to 375 public types, Components from 278 to 274 (most of what is left is parameters, models and Razor components, which are API by nature). (owner delegated, 2026-11-27, Phase 11f) | ZA-092, D18, D50 |
| D54 | **API conventions settled for v5 and checked by a test** ([Standards.md](Standards.md) §11). Recycle-bin flags are `IncludeDeleted` (also recycled) and `OnlyDeleted` (only recycled), `bool`, `false` by default, the shape `IncludeUnpublished`/`OnlyUnpublished` already had; `QueryContentParameters.IsDeleted` (`bool?`, the only tri-state) stays as an `[Obsolete]` alias that maps onto them, so what a query returns by default and for every old value is unchanged. `Cached` defaults are kept as they are (changing one changes what callers get) under a written rule: `false` on entity Get/Query parameters, `true` only on the small per-request lookups that are cleared on every write of their type; a test pins each. A public method takes its token last as `CancellationToken cancellationToken = default`, never `CancellationToken?` (`AuditExtensions.AddAudit` changed); `IContentService.ExportContentTypeAsync`/`ImportContentTypeAsync` gained one; `SaveChangesAndLog` and `IQueryModel.ExecuteQuery` got the default; exceptions: framework-fixed signatures, `ICacheService` (its load closes over the caller's token), and the token overload of `ISeoCheck.Check`, which keeps a token-less one. Discovery interfaces are not moved: Umbraco keeps its by feature, and moving `ISeoCheck` (in `Seo.Models`), `IStorageProvider` or `ITreeContextMenu` would break every plugin's `using` to buy neatness; the convention for new ones is `ZauberCMS.Core.<Feature>.Interfaces`. (owner delegated, 2026-11-27, Phase 11f) | ZA-124, D18 |
| D55 | **A site decides which page an address shows when no page has its URL, with content finders** (owner, 2026-11-27: build it; the design delegated, Umbraco's way). `IContentFinder` (`ZauberCMS.Core.Content.Interfaces`) is a discovery interface: finders are found like the other extension points (no registration), built for each lookup with constructor injection, and run by `SortOrder` (then type name) inside `IContentService.GetContentFromRequestAsync`, after the URL lookup finds no live page (the home page too) and before the not-found page (D41). The first result wins; null asks the next. A result names the page by id (or a `Content`) with an optional `LanguageIsoCode` and `RouteValues`; ZauberCMS loads the page again under the live rule (published, not recycled, not a block; internal redirect followed; the usual includes), so a finder can never serve an unpublished or recycled page: the next finder is asked instead. The route values reach the view through `EntryModel.RouteValues`, and `EntryPage` cascades the `EntryModel` to the view and layout. A finder that throws, or whose constructor throws, is logged as an error and passed over. The finder's answer is not cached (it caches its own search); the page it names is, under the Content prefix, as for a URL. The name is the one D52 removed: it is Umbraco's, and the old interface never ran, so a class written for it fails to compile instead of starting to run. Umbraco has `IContentFinder.TryFindContent(IPublishedRequestBuilder)` in an ordered `ContentFinderCollection` that composers edit (append, insert before), and `IContentLastChanceFinder` for 404s. ZauberCMS orders by a property because it discovers instead of composing (core principle: no manual registration; it ships no finder a site would need to remove); its last chance is `Zauber:NotFoundComponent`; and a finder names a page instead of setting one on the request, so the live rule is applied once by ZauberCMS, not by every finder. In the service, not a middleware: the request's culture (`CultureMiddleware`), `EntryPage`'s fallback and the language of interactive components (`ContentCultureCircuitHandler`) all ask the service, so they agree. (owner, 2026-11-27, Phase 11g) | Phase 11g, D41, D52 |
| D56 | **The very large services are split by concern into partial-class files; their types, constructors and members stay as they are.** `ContentService` (2877 lines in one file) is one `public partial class` in eleven files: the main file (constructor, get and query, 420 lines) and `.Save` (staging, drafts, versions, URLs, roles, values), `.Tree` (a page with its blocks), `.Delete` (recycle, restore, delete, orphans), `.Copy`, `.Routing` (the URL lookup and content finders, D55), `.ContentTypes`, `.Domains` (domains and page languages), `.ImportExport`, `.Access`, `.Trees`; none is over 760 lines. `MembershipService` (1248) became `public partial class` in three: users (552), `.Roles` (224), `.Account` (sign-in, registration, external sign-in, confirmation and passwords, 537). Separate internal services were set aside: they would change the constructors (a site that subclasses `ContentService` or builds it in a decorator calls them), move code that shares the private helpers into new types, and buy only a smaller class, which the files already give. Umbraco splits its `ContentService` by concern into several public services (`IContentService`, `IContentTypeService`, `IDomainService`, `IEntityService`, …); splitting `IContentService` the same way would break every site that injects it, for v5 with no gain the files do not already give. A test pins the shape (public, not sealed, one constructor, every interface method). (owner delegated, 2026-11-27, Phase 11g) | ZA-125, D18 |
| D57 | **`Content.Url` is the page's stored URL and is no longer obsolete; the `Url()` extension, which only returned it, is the obsolete one, and the column stays.** The property was `[Obsolete]` (pointing at `Url()`) and hidden from IntelliSense, yet it is the value EF maps, the front-end lookup compares, the save makes unique and the drafts, versions and exports carry, so every writer needed a pragma (16) and there was no exit: removing it would need a settable replacement under another name (every importer and starter kit changes, and the drafts' and versions' JSON would need its old name mapped) for no gain. Now readers and writers use `Content.Url` (`blog/my-post`, lower case, no slashes round it), a link is `ContentUrl.SiteRelative(content)` (`/blog/my-post`), and `Url()` is `[Obsolete]` with that message, for removal in a later major. No schema change and no migration: the column's name, meaning and data are unchanged. Umbraco keeps the stored segment (`UrlSegment`) apart from the computed link (`Url()` through `IPublishedUrlProvider`); ZauberCMS stores the whole relative URL, so its stored value and its link differ only by the leading slash, which `ContentUrl.SiteRelative` adds. (owner delegated, 2026-11-27, Phase 11g) | ZA-125, D18 |
| D58 | **A custom content component is given the id of its page (`ICustomContentComponent.ContentId`), not the page.** `RenderCustomContent` passed the whole `Content` (values, type, parent, roles) as a parameter, which Blazor serialises into the HTML for a component with `@rendermode` (the sample contact form: 7046 characters of protected descriptor, 777 with the id) and sends back with the circuit. A small DTO was weighed: the useful fields differ per component and any copy goes stale, while the id is enough to load the page through `IContentService.GetContentAsync(new GetContentParameters { Id = ContentId, Cached = true })` (published and not recycled by default, and cached). The admin has no custom components (the picker only lists their names), so nothing in ZauberCMS needed the page. Breaking (D18): the interface's `Content` property is replaced by `Guid ContentId`; no `[Obsolete]` path, because the renderer can pass only parameters a component declares and keeping `Content` would keep the serialisation it exists to stop. Umbraco renders view components on the server and passes `IPublishedContent` in memory; for Blazor's interactive components Microsoft's guidance is small serialisable parameters. (owner delegated, 2026-11-27, Phase 11g) | ZA-126, D18 |
| D59 | **Services keep a scope per operation for their DbContext; `IDbContextFactory<T>` is not adopted, and the pattern is no longer listed as a deviation.** The scope's `IZauberDbContext` is the context of the provider the site uses now, picked when the scope first asks (`ZauberSetup.ActiveContextType`): the installer writes the database settings and the next scope uses them without a restart, and a site with no database (D24) starts without one. `IDbContextFactory<T>` is typed to one context class fixed at registration, so it would need a factory of ZauberCMS's own that picks the provider, which is what the scope does. Identity's stores (`UserManager`, `RoleManager`) resolved from the same scope share its context, so one transaction covers a user and its content (ZA-011); contexts made by a factory would not be shared. Umbraco works the same way: a unit of work is a scope (`ICoreScopeProvider.CreateCoreScope()`) that owns the database connection, and services do not hold a context. Standards.md §3 states it as the pattern; the deviation row is gone. (owner delegated, 2026-11-27, Phase 11g) | Standards.md, ZA-011, D24 |
| D60 | **MVC's view features are registered for controllers, without a Razor view engine** (owner, 2026-11-27: "Register them"). `AddZauberCms` calls `AddMvcCore().AddViews()` after `AddControllers()`, so `[ValidateAntiForgeryToken]`, `[AutoValidateAntiforgeryToken]` and `[IgnoreAntiforgeryToken]` on a site's or plugin's controller work as in 4.x (since 11e they answered 500: their filters are view services); TempData, view components and HTML helpers come with them. The Razor view engine, tag helpers and the removed MVC rendering path (D52) stay out; a site with `.cshtml` views calls `AddControllersWithViews()`. `AuthController` keeps its own `IAntiforgery` check. Umbraco registers full MVC with views; ZauberCMS renders through Blazor, so it adds only what controllers need. | ZA-185 |
| D61 | **Each background job has an on/off switch under `Zauber:Jobs`, on by default** (owner, 2026-11-27: "Zauber:Jobs setting"). `Zauber:Jobs:DailyCleanup` (the daily job: audit rows, orphaned related-content links, page versions) and `Zauber:Jobs:SitemapGenerator` (the sitemap files); those are the only hosted jobs (the Redis cache-clear listener is plumbing every server needs, and the admin culture handler is a circuit handler, not a job). The job reads the switch from `IOptions<ZauberSettings>` when it starts (so code-set options work too) and, when off, logs one line and returns; it is still registered and stays internal. A web farm keeps `DailyCleanup` on one server; `SitemapGenerator` stays on wherever the sitemap file is served. Umbraco runs scheduled work only on its "scheduling publisher" server (server role, detected or set in config); ZauberCMS has no server roles, so the switch is per job and set by config. A new hosted job gets its own switch (a test lists them). | ZA-198 |
| D62 | **Work that must only follow a stored change runs in a new after-commit hook, `IAfterEntityCommit<T>`; `IAfterEntitySave<T>` keeps running inside the transaction.** `IAfterEntityCommit<T>` (`ZauberCMS.Core.Shared.Interfaces`, discovered like the other save hooks, constructor DI, `SortOrder`) has `Task AfterCommitAsync(entity, state, cancellationToken)` and gets every row the change wrote, each with the state it was saved in. It runs right after the save when no transaction is open, and when the caller's EF transaction commits otherwise; a rollback, a failed commit or a transaction disposed without a commit drops it. The commit is seen by an EF Core transaction interceptor the three ZauberCMS DbContexts add (a site's own context without it gets the hooks at the save, with a warning; an ambient `TransactionScope` is not seen). It cannot veto; an exception is logged and the other hooks still run, since the change is stored. Umbraco does the same: its "-ed" notifications (`ContentSavedNotification`, `MediaDeletedNotification`) are raised when the scope completes, and its file deletes run there. Why a new interface and not a changed one: `IAfterEntitySave<T>`'s contract (inside the transaction, can ask for a second save) is relied on by sites, and the two moments are both real needs (write more in the change / act once it is stored); a flag on the old interface would be a breaking change for every implementation. Async, because after-commit work is I/O. (owner delegated to Umbraco parity, Phase 10f-1) | ZA-163 |
| D63 | **The resized copies of an image are kept in a folder per image, and a permanent delete removes them.** ImageResize names a copy by a hash of the image's path, the size and the original's date and length, which can't be traced back, and offers no way to remove one image's copies; it does let the cache be replaced. ZauberCMS gives the resizer (`IImageResizerService`) a cache (`ResizedImageCache`, internal) that wraps the registered `IImageCache` and puts each copy in `{CacheRoot}/{shards}/{hash of the image's path}/`; reading, writing, the size limit and the start-up prune stay ImageResize's (the registered `IImageCache` is unchanged, so its prune still runs). `DiskStorageProvider.DeleteFile` removes that folder after the file, and `SaveFile` over a file of the same name removes the old copies. Rejected: clearing the whole cache on every purge (every image on the site would be resized again after each emptied bin), and changing the ImageResize package (another product's release). Umbraco keeps its ImageSharp cache outside the web root and leaves crops of deleted media until `CacheMaxAge` (a year); a delete for good (GDPR) asks for more, so ZauberCMS removes them. Copies on other servers of a web farm with a cache per server stay (they are never served through a resize URL again, the original being gone). (owner delegated, Phase 10f-1) | ZA-155 |
| D64 | **A front-end address is matched to a domain on its path below the site's path base, the same way everywhere, and the URL → page lookup is cached on that domain, the normalised slug and the flags.** The middlewares, `EntryPage` and a front-end circuit all match `example.com/fr` against the path without `PathBase` (components build the address with `NavigationManager.GetContentRequestUrl()`, public in `ZauberCMS.Core.Extensions`), so under `/site` the address `/site/fr/about` is on `example.com/fr` on every side; the stored domain needs no path base in it and keeps working when the site moves to another one. The lookup's cache key is what decides the answer: the matched domain's id (none: every host on no domain, which all share one entry), the slug in the stored form and `IsRootContent`/`IncludeChildren`/`IgnoreInternalRedirect`; the host, port, query string and spelling of the path are not in it, so an arbitrary Host header or a new spelling never makes a new entry, and only found pages are kept. Umbraco does the same: `UriUtility.UriToUmbraco` takes the application's virtual path off the request address before routing, its domains are matched on that address, and its published-content route cache is keyed on the domain's root and the path (`{root id}/{path}`), not the host. (owner delegated, Phase 10f-2) | ZA-187, ZA-190, D42 |
| D65 | **Every saved redirect is honoured: the compiled list holds all of them.** `RedirectMiddleware` loaded the newest 5000 (`QueryRedirectsParameters.Amount`'s default) and ignored the rest without a word. It now loads every row once into its compiled list (cleared by any redirect save, as before). Umbraco honours every redirect by looking the address up in its redirect table on each request (`IRedirectUrlService.GetMostRecentRedirectUrl`, by a hash of the route); ZauberCMS keeps the compiled list, because regex redirects and the domain rule (D42) need the whole list, and a query per request would cost every page a round trip and need a case-insensitive index on three providers (a migration). The list is what editors saved, not what visitors send, so its size is the site's own. Rejected: logging when the cap is hit (the redirects would still be ignored). The default of `Amount` (5000) stays for other callers. (owner delegated, Phase 10f-2) | ZA-189 |

## Working rules for the rework

1. One phase (or one finding group) per change; keep diffs reviewable. **Commit to `rework-2026` at the end of each
   phase** (owner instruction 2026-11-25), once it builds and is verified; push only when asked. Never commit
   `ZauberCMS.Web/app.db`.
2. Every fix: follow [Standards.md](Standards.md), grep all callers of anything whose signature/default changes,
   `dotnet build ZauberCMS.sln -c Release` clean, and state how it was verified (run the app for UI behaviour).
3. After fixing, take the finding out of the open list in `.audit/report.md` and add it, with how it was verified, to
   the phase's section at the top of `.audit/history.md` (keep the ID); update any doc the change makes stale.
   Re-run `/audit changed` at the end of each phase.
4. Schema changes: three migrations via `migrations.ps1` — and from Phase 6 on, verify on PostgreSQL too.
5. Public API (D18): everything ships as v5. Prefer additive changes and `[Obsolete]` where cheap; a break is allowed
   when it buys something, and goes in the phase's release notes ("Breaking:"). The big deliberate batch stays in
   Phase 11.
6. Add a regression test with each fix. Where a skipped test is already waiting for the finding
   (`Skip = "ZA-NNN (open)…"`), remove the skip in the same change.

## Phases (in order)

### Phase 0 — Critical hotfix — DONE 2026-11-25
- **ZA-044** fixed: `[Authorize(Roles = Admin)]` added to the three `/admin/seo/*` pages. Verified by running the
  app (anonymous requests redirect to login); details in [.audit/history.md](../.audit/history.md).
  ⚠ The hole is still in the **published 4.1.3 packages**: the fix is only on `rework-2026`. Owner decision D17:
  no hotfix; the fix ships with the v5 release.

### Phase 1 — Dependencies to latest on .NET 10 (D4) — DONE 2026-11-25
- Everything is on its latest version that runs on .NET 10 (Radzen 13, MailKit 5, HtmlSanitizer 11, ZauberCMS.RTE
  3.0.0, Microsoft.* 10.0.15, …). Build has 0 warnings; the vulnerability scan is clean. What was and was not
  tested is listed under ZA-130 in [.audit/history.md](../.audit/history.md) — read it before relying on an untested path.
- Held back on purpose: **Serilog.AspNetCore** stays on 10.0.0 (11.0.0 depends on Microsoft.Extensions 11.x) → Phase 12.
- **ZauberCMS.RTE**: migrated to the 3.x API on 3.0.0 in this phase; now on 4.0.0 (see "After Phase 4").
- **Blazored.Modal** (deprecated, repo archived) is now **vendored**: its source lives in `ZauberCMS.Core/Modal/`
  (same namespaces; moved to `ZauberCMS.Components/Modal/` in Phase 11e), the NuGet package is gone, and it is ours to maintain (ZA-133). Radzen was ruled out because its
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

### Phase 3 — Broken admin actions — DONE 2026-11-25
- Fixed: ZA-001 (recycle-bin restore / final delete / open), ZA-045 + ZA-088 (delete user), ZA-076 (Culture &
  Domains false error), ZA-103 (email confirmation, incl. change of email), ZA-058 (layout re-render reloaded
  editors), ZA-081 (silent context-menu failures), and ZA-137 (new: dashboard grids listed soft-deleted rows).
  Details and how each was verified: [.audit/history.md](../.audit/history.md).
- Public API additions (no breaks): `DeleteUserParameters.UserId` is `[Obsolete]` (use `Id`; still honoured);
  new `ConfirmEmailParameters.NewEmailAddress` and `DataGridParameters.IncludeDeleted`. Behaviour change for
  the release notes: `GetDataGridAsync` no longer returns soft-deleted Content/Media/User rows by default.
- Found, not fixed: ZA-138 (`SectionLayout` renders `<RadzenComponents>` twice, so every notification and dialog
  is doubled). Small; fix it whenever the owner wants.
- Not verified: a real confirmation email round trip; a forced context-menu failure in the browser.

### Phase 4 — Access control & identity — DONE 2026-11-25
- Fixed: ZA-053, ZA-052, ZA-059, ZA-002, ZA-020, ZA-019, ZA-003, ZA-060, ZA-132 (route half), ZA-021, ZA-022,
  ZA-005, ZA-138, and ZA-139 (new: content grids refused every filter). What each fix does and how it was
  verified: [.audit/history.md](../.audit/history.md).
- New open finding: ZA-140 — services do no authorisation of their own (the other half of ZA-132). Not done on
  purpose: a role check inside the services would break sites and plugins that save from the front end. It needs
  a permission design and the owner's decision.
- New settings (all three appsettings files, all with defaults that keep existing sites working):
  `Zauber:SiteUrl`, `Zauber:ForwardedHeaders` (`Enabled`, `KnownProxies`, `KnownNetworks`),
  `Zauber:HtmlSanitizer:AllowedIframeHosts`, `Zauber:Identity:ExternalProviders:Facebook|Microsoft:TrustEmail`.
- Public API additions (no breaks): `HandlerResult.RefreshSignInTicket`, `RefreshSignInTicketService`,
  `ZauberSignInManager.CreateExternalUserAsync` / `HasVerifiedEmail` / `EmailVerifiedClaimType`, `SeoHttp`,
  `RedirectTarget`, `AdminAccess`, `AdminAccessMiddleware`, `MediaSecurityHeadersMiddleware`, `SvgSanitizer`,
  `UploadContentCheck`, `ZauberSetup.BuildForwardedHeadersOptions`, settings classes
  `ForwardedHeadersSettings` and `HtmlSanitizerSettings`.
- Behaviour changes for the release notes (things a site owner or plugin author can notice):
  - A page under `/admin` with no `[Authorize]` of its own is now admin-only. A plugin page for another role must
    say so with its own `[Authorize(Roles = "…")]`.
  - With an admin IP allow-list, other addresses get 403 on every `/admin` request (was a forced sign-out on
    page load). Behind a proxy, enable `Zauber:ForwardedHeaders` or the proxy's address is what is compared.
  - Six wrong passwords lock an account for five minutes.
  - Facebook and Microsoft sign-up needs `TrustEmail: true`; Google works as shipped.
  - Rich text: iframes only from the allowed hosts; `data:` addresses only for bitmaps in images.
  - Uploads: text files that contain HTML or script are refused; files whose content does not fit the extension
    are refused for more formats than before.
  - SEO checker: private and loopback addresses are refused unless they are the site itself.
  - Grid filter/sort strings that touch secret columns or anything but plain columns throw `ArgumentException`.
  - Redirects: a target must be a local path or an http/https address; older rows that are not are skipped.
  - The sign-in cookie is re-checked every 5 minutes instead of 30 (one small query per signed-in user).
- Not verified: external sign-up against a real provider (owner will test, D8); anything behind a real proxy;
  an SVG upload through the admin UI (tested at the storage provider); tooltips and dialogs outside the Content
  section after the `<RadzenComponents>` change.
- Seen in this phase and handled afterwards ("After Phase 4"): the redirect editor's `/` prefix and the
  Spanish text in the admin.
- Also added in this phase, at the owner's request: the "pluggable and updatable" core principle in `AGENTS.md`.

### After Phase 4 — owner follow-ups — DONE 2026-11-25
- **ZauberCMS.RTE 4.0.0** (D12). Both csprojs bumped; the stylesheet link is gone from `App.razor` (the editor
  loads it). The CMS toolbar has the `embed` item. New `IRichTextEmbedService` / `DefaultRichTextEmbedService`
  (Core, replaceable): one provider list for the editor and for rendering; the default sanitiser expands
  placeholders after sanitising, so a site that renders rich text through `IHtmlSanitizerService` shows videos
  with no change. **ZA-134 fixed. ZA-136 fixed** (the 3.0.0 link workarounds are gone, `Rel` is pre-filled
  again); its last point, `data-contentid` lost when a content link is edited on the Manual tab, moved to ZA-135.
  Not taken: the other new toolbar buttons (indent, colours, find/replace, fullscreen, …) — owner's choice.
  The "3.1 upgrade brief" the 4.0 brief refers to was never given to this session; the package README was used.
- **Redirect editor** (D11): a full http/https target is kept as typed, anything else becomes a local path
  (`RedirectTarget.Normalize`), an invalid target shows a message. Found on the way and fixed (**ZA-142**): the
  redirect middleware kept its compiled list for five minutes, so a new redirect did nothing and a deleted one
  kept working until then; save and delete now drop the list (per process).
- **Spanish text in the admin** (**ZA-141**, fixed): relative dates and number/date formats in the admin followed
  the language of the root content, because `CultureMiddleware` treats the `/_blazor` connection request as a
  request for the home page and the circuit keeps that culture. The admin now always runs in
  `Zauber:AdminDefaultLanguage` (`AdminCulture`, `AdminCultureCircuitHandler`, and the middleware for `/admin`
  requests). The two layouts no longer set the process-wide default culture. Left open as **ZA-143**: front-end
  interactive components still get the root content's language, whatever page they are on.
- **ZA-140**: decided (D10), scheduled as Phase 10a.
- Build 0 warnings; 259 tests (256 pass, 3 skipped as before).
- Seen in the browser on the scratch database: existing YouTube iframe → embed card, foreign iframe → warning
  toast, new Vimeo embed through the button, block and page save, stored HTML holds placeholders only, public
  page shows both players; link create / re-edit (Rel pre-filled, no nested link); external, local and refused
  redirect targets, a new redirect working at once; admin dates in English with a Spanish root.
- Not verified: two editors on one page and an editor in a *nested* block side panel (one editor in a block
  side panel was checked); dark theme; a site with its own `IHtmlSanitizerService`.
- Release notes: content with YouTube/Vimeo iframes is rewritten to placeholders the next time an editor opens
  and saves it, and iframes from other hosts are then lost (they are still rendered until that save if the host
  is in `Zauber:HtmlSanitizer:AllowedIframeHosts`); a custom `IHtmlSanitizerService` must call
  `IRichTextEmbedService.Expand`; rendered embeds are `figure.zauber-embed`; the admin ignores the browser's
  and the content's language; public API additions `IRichTextEmbedService`, `DefaultRichTextEmbedService`,
  `AdminCulture`, `AdminCultureCircuitHandler`, `RedirectTarget.Normalize`, `RedirectMiddleware.ClearCache`.

### Phase 5 — Content integrity — DONE 2026-11-25
- Fixed: ZA-046 (duplicate URL breaks front-end), ZA-047 (drafts discarded), ZA-048 (recycle parent/children),
  ZA-049 (copy shares draft), ZA-065, ZA-066, ZA-067 (publish/slug/nested-URL leaks), ZA-070, ZA-074, ZA-075,
  ZA-079, ZA-016, ZA-135 (rich-text content links). What each fix does and how it was verified:
  [.audit/history.md](../.audit/history.md). Build 0 warnings; 315 tests (312 pass, 3 skipped as before). `/audit changed` found six more
  problems in the change: four fixed, two logged as new Low findings ZA-144, ZA-145.
- No schema change. Public API additions (no breaks): `ContentUrl` (Core, `ZauberCMS.Core.Content`),
  `ContentMappingExtensions.MapDraftTo`, `InsertContentLinkItem.FindContentId`, `ContentLinkDialog.ExistingContentId`;
  XML docs on `SaveContentParameters` and `QueryContentParameters.IsDeleted`. `RestoreContextMenu` gained a
  `NotificationService` constructor parameter (built by discovery; only code that news it up is affected).
- Behaviour changes for the release notes:
  - Recycling a page recycles everything below it; restoring it restores the pages that went with it (not ones
    binned on their own earlier); a page can't be restored while its parent is in the bin; permanently deleting a
    recycled page deletes the recycled pages below it. The recycle bin lists only the top-most recycled items.
  - Rich text: links to recycled pages are kept (they were removed when the editor opened).
  - Saving a page with a URL that another live page has is refused; generated, copied and restored URLs get a
    `-1`, `-2`… suffix. Copies are `<url>-copy` (suffixed when taken) and no longer share the original's draft.
  - Sort, move, restrict access, culture and restore (saves with `ExcludePropertyData`) keep a pending draft.
  - Request slugs are matched case-insensitively and without leading/trailing slashes on every provider.
  - Blocks get no URL and are never served as pages; an unpublished domain root or redirect target is not served;
    `Children` on the front end and in queries never contain recycled (and on the front end, unpublished) items.
  - Restoring a version changes name, navigation settings, values and blocks only, and discards the pending draft.
  - A content type used by drafts or recycled pages can no longer be deleted (it silently deleted them before).
  - Moving a page under its own descendant is refused; moving rewrites the stored paths below it.
- Not verified: draft kept across a sort and then published in the browser (service tests only), version restore
  from the Versions tab, content-type delete and copy from the admin menus; anything on PostgreSQL/SQL Server.
- Left as it was: with `EnablePathUrls`, a moved page keeps its URL (URLs are built only on create). ZA-143 says
  "Phase 5" in the report but was never in this phase's list; it is still open.

### Phase 6 — PostgreSQL & Redis made real — DONE 2026-11-25
- Fixed: ZA-050 (PostgreSQL install failed; unique version indexes missing everywhere), ZA-007 (raw SQL on
  PostgreSQL), ZA-017 (search under a Turkish server culture), ZA-122 (design-time factory file name), ZA-051,
  ZA-014, ZA-055, ZA-072 (cache redesign, D13) and ZA-143 (non-page requests looked up as the home page; front-end
  circuits in the root's language). Details: [.audit/history.md](../.audit/history.md).
  `/audit changed` (two independent reviewers) found 14 more problems in the change: 12 fixed (cache index races,
  Redis resync and counter reset, failed publishes, PostgreSQL test connections, the upgrade keeping the wrong
  version, the remaining culture-dependent `ToLower` calls, path base, filtered-include keys, clears after commit),
  two logged as ZA-146 and ZA-147 (Low). ZA-147 was then checked in the browser with the sample site and is not a
  bug (see [.audit/history.md](../.audit/history.md)).
- Test infrastructure (D15): Testcontainers PostgreSQL + Redis, shared by the run; the content, data-grid, media,
  user-delete and provider-query test classes also run on PostgreSQL; Redis tests use two sites as two servers.
  CI builds and tests on every push; on CI the container tests fail instead of skipping. Build 0 warnings; 428
  tests (425 pass, 3 skipped as before).
- ZA-050 reproduced first: an empty PostgreSQL database failed `Initial` with `42601: syntax error at or near "["`.
  Per D14 the PostgreSQL `Initial` migration was corrected (index filter only), and migration
  `ContentVersionIndexes` (all three providers) adds the unique "current published version" index after keeping only
  the newest flagged version per page.
- App run on PostgreSQL + Redis (containers): migrations applied, registration, admin, a settings save published an
  invalidation through Redis; no errors in the log.
- SQL Server (owner's local server, after the phase commit): the same test classes run on SQL Server through
  `ZAUBER_TEST_SQLSERVER` — 78 cases, all pass (migrations on an empty database, the upgrade with duplicate flags,
  the unique index, raw SQL, search). App run on a new SQL Server database: migrations, the unique index,
  registration, admin, a settings save; no errors in the log.
- Schema: one migration, `ContentVersionIndexes`. Public API:
  - Added: `ServerCache` (its `Remove*` methods apply a clear without publishing, for a custom bus),
    `ICacheInvalidationBus` (replaceable backplane), `CultureMiddleware.IsContentEndpoint`,
    `ContentVersionDbMapping.UniqueCurrentPublishedIndex` / `UniqueLatestDraftIndex`.
  - Breaking (D18): `DefaultCacheService`'s constructor is now `(ServerCache, ICacheInvalidationBus)`. Only code that
    creates or subclasses it is affected; resolving `ICacheService` is unchanged.
- Behaviour changes for the release notes:
  - PostgreSQL installs work. Upgrading removes duplicate "current published" flags on old versions (the newest
    stays) and adds a unique index for it.
  - With `RedisConnectionString`, cached data stays in each server's memory and Redis only carries the clears; nothing
    cached (users, password hashes, pages) is written to Redis any more. Sites sharing one Redis server should use
    different application names.
  - Content search and the admin user search ignore case whatever the server's culture; slugs and file extensions
    are lowered the same way in every culture (a Turkish page named "Indigo" gets `indigo`, not `ındigo`).
  - Requests that are not pages (`/_blazor`, a site's own endpoints, 404s) are no longer looked up as the home page.
    Interactive components on a front-end page run in that page's language (they ran in the home page's).
  - Version flags are switched inside the save's transaction (a save that fails leaves the old flags).
  - Content saves, recycle, purge and content-type delete clear the content cache again after they commit.
- Not verified: SQL Server on CI (local only); the Redis behaviour when Redis drops mid-run and comes back
  (reconnect path, read but not exercised); creating content through the admin UI on PostgreSQL (service tests only);
  a real multi-server deployment (two sites in one test process stand in for it).

### Phase 7 — Installer (D2, D19–D24) — DONE 2026-11-26
- Fixed: ZA-004 (first registered user became admin), ZA-023 (admin-list promotion without proof of the address),
  ZA-024 (registration flooding), and ZA-148, found while testing this phase (the home page `/` did not run in its
  content language since the ZA-143 fix). Details: [.audit/history.md](../.audit/history.md); how it works:
  [Installer.md](Installer.md).
- What was built:
  - A site with empty `Zauber:DatabaseProvider` / `ConnectionString` starts without a database. `InstallerMiddleware`
    (in front of authentication) serves `/install` and sends everything else there until the site is installed;
    afterwards it does nothing and `/install` is an ordinary address.
  - Database step: SQLite / SQL Server / PostgreSQL by fields or connection string, connection test, migrations,
    then `appsettings.json` is written. When the file cannot be written or something overrides it, the page shows
    the values to set by hand (the open point from planning).
  - One form for administrator, site name, language, registration switch and starter content, saved in one go.
    The administrator is signed in and lands in `/admin`.
  - "Installed" is a `GlobalData` row (`ZauberInstalled`), never unset; a database with any user gets it at
    start-up (upgrade). Recovery key `Zauber:Identity:RecoverAdminUser`.
  - Starter content: discovery interface `IStarterKit`; the kit and its views ship in the **template** project
    (two page types, two pages), not in the packages, so a site owns and edits them. A site made without the
    template is offered "An empty site" only.
  - Registration: `GlobalSettings.AllowRegistration`, honeypot + minimum fill time (`IFormProtectionService`,
    `<FormProtectionFields />`), rate limit policy `"register"`, discovery interface `ICaptchaProvider`.
- **Different from D19 as written: no restart.** The DbContexts already read the connection string from
  configuration on every use, so only the provider had to become a per-scope choice. The installer writes
  `appsettings.json`, reloads configuration and carries on. A restart would have ended the process under plain
  Kestrel (`dotnet run`, containers without a restart policy). A site that starts with a database registers only
  its own provider and behaves as before.
- **Different from D22 as written: a confirmation link does not promote.** D22 says an Admin Email Address is
  promoted "only after the email is confirmed". Built that way first, the audit showed it is not safe: a stranger
  registers with an administrator's address and a password of their own, the administrator (or a mail scanner)
  opens the confirmation link, and the stranger's account is an administrator. So a listed address is promoted
  only when the account's holder proves the address: a social login with a verified address, or setting the
  password from an emailed link (the registration sends that link; "Forgot your password" does too).
- **D24 (owner, during this phase):** no switch to turn the installer off; a site that loses its database
  settings shows the installer. Logged as an error at start-up and documented.
- Also added because the installer asks for them: `GlobalSettings.SiteName` and `GlobalSettings.DefaultLanguage`.
  The second is the language of pages that have none on themselves, an ancestor or their domain; before, those
  used the admin language.
- Schema: none. Public API:
  - Added: `ZauberCMS.Core.Installer` (`IInstallService`, `InstallService`, `IStarterKit`, `InstallState`,
    `InstallerMiddleware`, `InstallParameters`, `DatabaseSetupParameters`, `InstallResult`,
    `DatabaseSetupResult`, `DatabaseTestResult`, `InstallFlag`, `StarterKitContext`; the installer's Razor
    components with `InstallerPageModel`, `InstallerStep` and `InstallerOption` are public but not for use by
    sites), `ZauberCMS.Core.Data.DatabaseProviders`, `ZauberCMS.Core.Languages.CultureLookup`,
    `ZauberCMS.Core.Security` (`ICaptchaProvider`, `IFormProtectionService`, `FormProtectionService`,
    `FormProtectionResult`), `RoleExtensions.PromoteListedAdminAsync` / `IsAdminEmailAddress`,
    `ProviderService.CaptchaProvider`, `ZauberSetup.RegisterRateLimitPolicy`, `FormProtectionFields` component,
    `GlobalSettings.SiteName` / `DefaultLanguage` / `AllowRegistration`, `ZauberSettings.FormProtection`
    (`MinimumFillSeconds`), `ZauberSettings.Identity.RecoverAdminUser`, `ZauberSettings.Plugins.CaptchaProvider`.
  - Breaking (D18): none in signatures. `IZauberDbContext`, `IUserStore<User>` and `IRoleStore<Role>` are now
    registered by factory; code that replaced those registrations after `AddZauberCms` still wins.
- Behaviour changes for the release notes:
  - A new site (template or packaged `appsettings.json`) starts with the installer; the database keys ship empty.
    Existing sites are marked installed on first start (a `ZauberInstalled` row is written) and never see it.
  - Empty `Zauber:DatabaseProvider` or `ConnectionString` is no longer a start-up failure: the site starts and
    shows the installer to every visitor (D24). **After installing on a server, put the two settings into the
    project's `appsettings.json` or the environment before publishing again.**
  - Until a site is installed every request, a site's own endpoints and static files included, is answered by
    the installer.
  - **The first account to register is no longer an administrator.** There is no unattended install. An automated
    set-up creates a user itself (an `ISeedData`; the site then counts as installed) and names it in
    `Zauber:Identity:RecoverAdminUser`.
  - **Plugins:** a hosted service that uses the database when it starts must check
    `InstallState.IsDatabaseConfigured` first, or it stops the host of a site that has no database yet. On such a
    site the built-in daily clean-up and sitemap jobs do their first run a day and six hours after the install.
  - An address in Admin Email Addresses becomes an administrator only through a social login with a verified
    address or by setting the password from an emailed link; a registration with such an address is sent that
    link. The comparison ignores case.
  - With registration turned off, a first sign-in through an external provider creates no account.
  - The registration page refuses a form sent back in under 3 seconds, or with the hidden field filled, and
    allows 5 attempts per address in 10 minutes (it shared the login allowance of 10 in 5 minutes).
  - The home page runs in its content language again (ZA-148).
  - A page with no language anywhere uses `GlobalSettings.DefaultLanguage` when it is set.
- `/audit changed` (three independent reviewers: security, correctness, API and docs) found 3 problems worth
  High and about 30 smaller ones in the first version of this change. Fixed before the commit: the admin-list
  promotion (above); an install that could be left half-done by a closed tab, with registration open against
  the installer's choice (settings now go in before the account, no cancellation after it, a failed account is
  removed); a provider that stuck after one wrong value; "has users → installed" firing in the middle of an
  install; seeders able to run twice at once; `appsettings.json` losing its permissions when rewritten, and
  breaking when it used flat `Zauber:Key` names; a starter kit that cannot be created taking the installer down;
  the installer's GET having no error page; `RecoverAdminUser` matching a user name that looks like an email
  address, and leaving an unconfirmed account unable to sign in; `IMembershipService.ExternalLoginAsync`
  ignoring the registration switch; the settings key for the fill time renamed before it ships
  (`Zauber:FormProtection:MinimumFillSeconds`); `DatabaseProviders` moved to `ZauberCMS.Core.Data`; accessibility
  and wording of the installer pages; the template linking pages by hand. Decided by the owner: D24. Logged as
  new Low findings: ZA-149…ZA-152. The line numbers of 21 open findings in files this phase changed were brought
  up to date.
- Tests: 628 (528 pass; 100 skipped: 97 SQL Server variants, which need `ZAUBER_TEST_SQLSERVER`, and the 3 for open
  findings). New: `tests/ZauberCMS.Core.Tests/Installer/`, `Membership/StartingRoleTests.cs`,
  `Security/FormProtectionTests.cs`; additions to `ExternalSignUpTests`, `ContentCultureTests` and
  `ServiceContainerTests`. Build 0 warnings.
- Checked in a browser (scratch sites, nothing in the repository's `app.db`): a site with no database → database
  step → SQLite, and separately PostgreSQL in a container (database created by the installer), both without a
  restart; a site whose connection string is set and whose database is empty (tables created at start-up, the
  installer starts at the set-up form); refused weak password; install; signed in at `/admin`; starter site from
  a copy of the template built against this source (`/` and `/about` in the chosen language); registration off →
  404 and no link; registration on → too fast, honeypot, success as Member, 429 after 5 posts; the "set it by
  hand" screen; narrow screen; an existing database (copy of the dev site's) → marked installed, `/install` is
  404. After the audit fixes: the database step's error state, and the whole install with the starter kit on
  SQLite again.
- Not verified: the installer with scripts turned off (built to work as one long form; not opened that way);
  SQL Server through the installer (the SQL Server test variants were skipped here: no `ZAUBER_TEST_SQLSERVER`);
  the "choose a password" email in a real mailbox (a test records that it is sent and where its link points);
  the registration page and the "set it by hand" screen in a browser after the audit fixes (their tests pass);
  a real CAPTCHA provider; `RecoverAdminUser` on a running site (tests only); a host where `appsettings.json` is
  read-only (the override case was used to reach the same screen); the published template package (it cannot
  build until the packages with these APIs exist — see [Releasing.md](Releasing.md) for the check that was done).

### Phase 8 — Media recycle bin (D3) — DONE 2026-11-26
- Fixed: ZA-006 (no media recycle bin), ZA-013 (media events before the save, or twice), and two found on the way:
  ZA-154 (restricted media could be fetched with a doubled slash in the address — older than this phase, found by
  its audit) and ZA-153 (the installer's SQL Server connection test, found by running the SQL Server tests for the
  first time). Details: [.audit/history.md](../.audit/history.md); how it works: [Services.md](Services.md) "Deletes".
- What was built:
  - Media section → **Recycle Bin** under the media tree. Delete moves an item, a folder with everything in it, to
    the bin. On a binned item: Restore, Delete permanently; on the bin: Empty recycle bin. The bin's menus are
    `ITreeContextMenu`s for `MediaRecycleBinTree`, so a plugin can add its own.
  - Restore brings back what went to the bin with the item (one timestamp, as for content) and is refused while
    the parent folder is still in the bin.
  - A permanent delete removes the rows and the files (and the default disk provider's empty `media/{id}/`
    folder). The confirmation names the pages, blocks and members whose property values still point at the media.
    A file that can't be removed stops the delete, so no file is left on disk without its row (it would be public).
  - Recycled media is served to administrators only; everyone else gets 404. Restricted and binned files are sent
    with `Cache-Control: private, no-store`.
  - Saving media that is in the bin, or into a binned folder, is refused (a dialog opened before the delete).
- As in D3, a permanent delete straight from the media tree is allowed by the service (`MoveToRecycleBin =
  false`) but has no menu item: the admin always goes through the bin. Not built, so nothing to decide unless
  wanted.
- Schema: none. Public API:
  - Added: `IMediaService.RestoreMediaAsync`, `EmptyMediaRecycleBinAsync`, `GetMediaUsageAsync`;
    `RestoreMediaParameters`, `EmptyMediaRecycleBinParameters`, `GetMediaUsageParameters`; `MediaUsage`,
    `MediaUsageSource`; `RestrictedMediaEntry.InRecycleBin`; `AuditAction.Restore` (added at the end);
    `Constants.Guids.MediaRecycleBinRootId`, `Constants.Sections.Trees.MediaRecycleBinTree`; components
    `MediaRecycleBinTree`, `RestoreMediaContextMenu`, `DeleteMediaPermanentlyContextMenu`,
    `EmptyMediaRecycleBinContextMenu`.
  - Breaking (D18): `IMediaService` has three new members (a class implementing or decorating it must add them);
    `MediaService` takes an `ILogger<MediaService>`; `DeleteMediaContextMenu` no longer takes `IMembershipService`
    and `AppState`; `RestrictedMediaEntry` has a third positional parameter (source compatible, binary break).
- Behaviour changes for the release notes:
  - Deleting media moves it, and everything in a folder, to the recycle bin; before, a folder with media in it
    could not be deleted, and deleted media could not be brought back or removed.
  - `DeleteMediaParameters.DeleteFile` now defaults to `true`: a permanent delete removes the file unless the
    caller says otherwise. A permanent delete takes the binned items below with it and is refused while anything
    below is live.
  - `SaveMediaAsync` no longer moves media in or out of the bin (it warns when asked to), refuses to change binned
    media, and refuses to save into a binned folder. A folder's included `Children` no longer contain binned items.
  - Media in the recycle bin is no longer served to visitors (404), whether or not it was restricted; pages that
    still use it show a missing image. A CDN keeps copies it took while the file was public until it is purged; a
    storage provider that serves files from its own address must keep binned files private itself.
  - Media events (`AppState`) are raised once, by the service, after the save succeeded.
- `/audit changed` (two independent reviewers: correctness and data; security, admin UI and API) found ZA-154 and
  about ten smaller problems in the first version. Fixed before the commit: the doubled-slash bypass; caching
  headers; a file left without its row after a failed delete; saving a binned item moving it off its group's
  timestamp (Restore then left its contents behind); uploads into a binned folder; binned children in a folder's
  `Children` (the Sort dialog saved them); the usage check's cost (fewer terms, parameterised, the whole bin checked
  on the server); a shared file matched only by exact URL; the release-notes list. Logged as new Low findings:
  ZA-155 (resized copies stay in `_mediacache`), ZA-156 (bin lists 500 items, a binned folder can't be opened),
  ZA-157 (save hooks don't see the items below or an emptied bin).
- Tests: see the run log in [.audit/history.md](../.audit/history.md) for the count. New: `tests/ZauberCMS.Core.Tests/Media/MediaRecycleBinTests.cs`;
  additions to `RestrictedMediaTests`. **The SQL Server variants of every test now ran for the first time**, on
  LocalDB (`ZAUBER_TEST_SQLSERVER=Server=(localdb)\MSSQLLocalDB;Integrated Security=true;TrustServerCertificate=true`),
  which found ZA-153. Build 0 warnings.
- Checked in a browser (scratch copy of the sample site with its own `wwwroot`, nothing in the repository's
  `app.db` or media): folder to the bin (tree and menus), recycled file 404 for anonymous requests in any case and
  through the resizer and 200 for the administrator, restore, permanent delete with "Still used by 1 page or block:
  Home", empty bin with a folder of two files, files and folders gone from disk, audit rows. Not re-checked in the
  browser after the audit fixes (their tests pass): the dialog wording and the refused save of a binned item.

### Phase 9 — Block List redesign (D5) — DONE 2026-11-26
- Fixed: ZA-008, ZA-009, ZA-010, ZA-034 … ZA-043 (all the block-list failure modes F1–F13). How it works now:
  [BlockListEditor.md](BlockListEditor.md); the old design's failures and where each is fixed: its §6 table.
- What was built (no schema change, no new setting):
  - **Edit session**: `ContentEditScope`. The page editor holds the root scope; every block side panel gets a child
    scope that the list commits on Save and rolls back on Cancel or close. Nothing is stored until the page is saved.
  - **One save**: `IContentService.SaveContentAsync` (with the new `SaveContentParameters.Blocks`) saves the page and every block edited below it in one
    transaction, deletes blocks no longer reached (with everything below), one audit row, one version taken after the
    blocks are written. "Save only" of a published page gives the page and its edited blocks drafts; the live site
    doesn't change until publish, which also publishes the blocks' drafts. Clear changes and version restore handle
    block drafts too.
  - **Static previews**: `BlockPreview` renders the site's `IContentBlockPreview` to HTML on the server, inert, in a
    shadow root with the editor's stylesheets (no Blazor nodes in the shadow root, no global `<link>`). Previews
    show unsaved nested blocks at any depth through `GetBlocks(…, includePendingChanges: true)`.
  - **`INestedContentReferenceProvider`** (discovered, by editor alias; the Block List's built in): how Core finds the
    blocks a value owns. Used by the tree save, permanent delete, version snapshots and restore, copy and the
    allowed-types check, so blocks in compositions and drafts are included and pages a picker points at are never
    treated as blocks. A custom nested editor (grid, layout) gets save/delete/version/copy by implementing it.
- Owner's concern during the phase (nested lists within lists, and showing changes before save, "always a problem"):
  the history was searched for every old problem and workaround (20 found; the redesign covers them, six risks
  found and fixed or documented, see the audit section below), and the editor was checked in a browser four levels
  deep (FAQ → item → FAQ → item), with a site stylesheet on level 3.
- Schema: none. Public API:
  - Added: `SaveContentParameters.Blocks`, `INestedContentReferenceProvider`,
    `BlockListNestedContentReferenceProvider`, `ContentMappingExtensions.MapBlockDraftTo`, the many-entity
    `SaveChangesAndLog` overload, `ContentEditScope`, `BlockPreview`, `ContentEditor.EditScope`,
    `BlockListItem.PreviewContent`. `CopyContentAsync` now returns the copy as `Entity`.
  - Obsolete (never called now): `ISupportsBlockListChanges`, `BlockListChanges`, `ContentEditor.OnBlockListChanges`,
    `DynamicContentProperty.ChangesPending`, `RenderBlock.Stylesheets`.
  - Breaking (D18): `BlockListState` lost its change tracking (`AddedItems`,
    `UpdatedItems`, `DeletedItems`, `GetChanges`, `MergeNestedChanges`); `BlockListEditorProperty` no longer
    implements `ISupportsBlockListChanges`; `RenderBlock` lost its injected `JsRuntime`/`Logger` properties;
    `shadow-dom.js` (and its global `initializeShadowDOMWithMultipleStylesheets`) is replaced by `block-preview.js`.
- Behaviour changes for the release notes:
  - Block edits are saved with the page, all or nothing, and a cancelled block panel keeps nothing. "Save only" no
    longer changes live blocks.
  - Admin block previews are static HTML: no interactivity, JS, `OnAfterRender` or admin cascading values in a
    preview, and clicks in it do nothing. A preview that should show unsaved nested blocks calls
    `GetBlocks(alias, ContentService, includePendingChanges: true)`; that call now also shows the blocks' stored
    drafts and unpublished blocks, as the editor does.
  - The Block List editor's `Stylesheets` setting styles only the previews (no `<link>` in the admin page).
  - Version history: a page's version now holds its blocks as that save wrote them, and one save makes one version
    (blocks get none of their own). Restoring a version also drops its blocks' drafts and deletes blocks the restored
    values don't use. Snapshots, delete and copy only follow block editors (through the provider): a JSON array of
    ids in another property is no longer treated as blocks.
  - A copied block belongs to the copy (`RelatedContentId`); before, copies kept the original page's id.
  - A permanent content delete notifies with the deleted item instead of `null`, and an open editor reloads only for
    a change to its own item (with a warning when that drops unsaved block edits).
  - A "Save only" (or publish) from a copy opened before someone else's "Save only" is refused instead of overwriting
    that draft.
  - `SaveContentAsync` of an item's values now saves it with its blocks: publishing a page from code also publishes
    its blocks' pending drafts and deletes blocks the page no longer uses; a "Save only" of an item that was never
    saved is refused; `IBeforeEntitySave<Content>` / `IAfterEntitySave<Content>` now get Added/Modified (before:
    Unchanged) and a veto stops the save.
- Owner decision (2026-11-26, after the review): one save method. Every `SaveContentAsync` of an item's values goes
  through the page-with-blocks save (the briefly added `SaveContentTreeAsync` was folded in before release), so code
  that publishes a page also publishes its blocks' drafts and removes unused blocks (fixes ZA-158), and content saves
  run the save hooks with the right state (most of ZA-012). Saves with `ExcludePropertyData` keep their own path.
- Not done: Clear changes and version restore don't run save hooks per block (ZA-159).
- Tests: `ContentTreeSaveTests` (Core, 22 per database: nested add, removal with subtree, two lists, cancelled and
  re-edited blocks, all-or-nothing, drafts with publish and clear at depth, stale draft refused, version restore
  incl. an old-style version, delete, copy, the 20-level limit), `ContentEditScopeTests`, `BlockListStateTests`.
- Checked in a browser (scratch copy of the sample site): nested add two and four levels deep, cancel at every
  level, re-edit before save, previews updating before save at every level, save twice without reload, front end,
  "Save only" (live unchanged, editor shows the draft), Clear changes, delete a block with children (no orphan
  rows), version restore, drag reorder, the stylesheet shadow root; after the review fixes, the inert previews, the
  labelled edit/delete buttons, and the warning in one tab when another tab saves the page under unsaved block edits.
  Not checked in a browser: the warning in an open panel when its page reloads underneath it (same mechanism).

### Phase 10 — Performance & async — DONE 2026-11-26
ZA-057, ZA-061…ZA-064, ZA-056, ZA-068, ZA-069, ZA-071, ZA-073 (queries/caching); ZA-054 + ZA-082 with the
`PaginatedList` async refactor (must land together); ZA-083, ZA-084, ZA-086, ZA-080; then the
`IServiceScopeFactory` → `IDbContextFactory` / single-context refactor (ZA-011).

Done so far (2026-11-26), in steps:
- **Step A — front-end caching and queries** (ZA-056, ZA-057, ZA-061 … ZA-064, ZA-071, ZA-083, ZA-084): the URL →
  page lookup is cached 5 minutes on host + path (not the query string), with language keys applied outside the
  cache; block lists, media pickers and the 404 check are cached; `ContentTypeAlias` filters in SQL; cache keys are
  built only when caching; views, block views and editor assets are discovered once, not instantiated per request;
  role queries load members only on request; dictionaries load in three queries; the audit cleanup is one `DELETE`
  and the daily job starts five minutes after start-up; uploads stream to disk and log their errors.
- **Step B — versions and bulk operations** (ZA-069, ZA-073; D25): version retention, a lighter version list,
  public access set in one transaction, copy one query per block level, the sitemap pages through every page.
- **Step C — async paging and admin trees** (ZA-054, ZA-068, ZA-082; D26): `ToPaginatedListAsync` and async EF calls
  throughout the services; content and media trees load a page of a node at a time with its child flags and a
  "Show N more" row (also the recycle bin and the media folder grid); content-type trees load once and expand from
  memory; `IDataListSource.GetItemsAsync`. Checked in a browser on a scratch copy of the sample site: 107 pages under
  one page and 106 items in a media folder paged with "Show more", expand arrows right (an empty folder has none),
  context menus and Public Access on a tree node, expanded nodes kept open after a refresh, the three structure trees,
  the recycle bin. Not clicked: the recycle bin's own "Show more" (no 100 binned items), the pickers in dialogs (same
  component), a list editor on a content-type data source.
- **Step D — circuits and contexts** (ZA-080, ZA-086, ZA-011): the version panel reloads only for its own page on
  the circuit's dispatcher; `UserManager` is resolved per operation in services; one DbContext per scope shared by the
  services and Identity. Not done: replacing scope-per-method with `IDbContextFactory` everywhere (no finding asks
  for more than one context per scope; it stays in Standards → Known deviations, for Phase 11 if wanted).
- Review (`/audit changed`, two reviewers): fixed — the bin trees wiped the content and media trees' child flags
  (each tree now keeps its own), the page cache never expired on a busy page and kept misses (5-minute key window,
  misses not cached, role, content type and language saves clear it), overlapping bin refreshes, "Show more" twice,
  the media folder grid folding back, the bin's "Show more" menu and selection, content-type child levels, release
  notes. Recorded: ZA-161 (the highlight after "Show more"). Not changed: the reviewer's "public access no longer
  notifies" (the menu still raises the event) and "editor assets left the front end" (they were admin-only before).
- Tests: `QueryAndCacheTests` (Core, all three databases). Not covered by a test: the upload streaming (needs an
  `IBrowserFile`), the sitemap generator (a hosted service), the per-request view discovery (build only).
- **Release notes (D18):**
  - `Zauber:ContentVersionsToKeep` (default 50): existing sites start removing older versions on upgrade, on the
    first daily run; set 0 to keep everything.
  - `GetRoleAsync`/`QueryRolesAsync` no longer load `Role.UserRoles`; pass `IncludeUsers = true`.
  - `GetContentVersionsAsync` returns untracked versions whose `CreatedBy` has only id, name and email.
  - Public access ("restrict access") no longer saves each page below through `SaveContentAsync`: no Content save
    hooks and no versions for it; the `ContentRole` hooks run instead. New: `IContentService.SetContentRolesAsync`,
    `IContentVersioningService.PruneVersionsAsync` (implementers of these interfaces add them).
  - The front-end page lookup is cached up to 5 minutes instead of 5 seconds (a fixed 5-minute window, not
    sliding; a lookup that finds no page is not cached). Content, domain, content type, role and language saves clear
    it at once; changes made straight in the database, or on another server without `RedisConnectionString`, show
    within those 5 minutes. New: an `ICacheService.GetSetCachedItemAsync` overload with a `shouldCache` filter (a
    default method: existing cache services keep working).
  - Step C: `ToPaginatedList`, the `PaginatedList(IQueryable…)` constructor and `BuildPath` are obsolete (use
    `ToPaginatedListAsync`, `BuildPathAsync`). New: `IContentService.GetContentTreeLevelAsync`,
    `IMediaService.GetMediaTreeLevelAsync` (implementers of these interfaces add them),
    `IDataListSource.GetItemsAsync` (a default method: existing sources keep working). Admin tree nodes no longer
    carry property values or children (a custom `ITreeContextMenu` that read them loads the item by id); content
    pickers' trees now list unpublished root pages too. `ContentTree`/`MediaTree` refresh themselves; a section
    that passed `Data` no longer needs to; `MediaTree` no longer has public `ContextMenuService` and
    `NotificationService` properties. The list editors load their data source after the first render:
    `ListPropertyBaseComponent` does its setup in `OnInitializedAsync`, so a subclass that read `SettingsModel` after
    `base.OnInitialized()` reads it in `OnInitializedAsync` after `await base.OnInitializedAsync()`. The built-in trees
    keep their own "has children" flags; `TreeState.HasChildrenCache` is no longer filled by them.
  - Step D: `ContentService` and `EmailService` constructors no longer take `UserManager<User>` (subclasses
    change their base call). Within one DI scope the services and Identity now share one DbContext: a `SaveChanges`
    on `IZauberDbContext` also saves changes Identity has tracked in that scope (Identity saves its own at once, so
    normally there are none).
  - `AnyContentAsync` ignores content in the recycle bin. `DiskStorageProvider` reports a generic error message and
    logs the detail; its constructor has an extra optional `ILogger` parameter (code that constructs or subclasses it
    recompiles). Sitemap `priority` is written in the invariant culture and `lastmod` in UTC.

### Phase 10a — Who is calling (D27) — DONE 2026-11-26
Planned as service authorisation (D10, ZA-140) with ZA-093. The owner weighed the permission hook against what it
buys and dropped it (D27): services trust their caller, and the page, component or endpoint checks access. What
was built is the part with real bugs behind it:
- **`ICurrentUserAccessor`** (`Membership/Interfaces/`, default `CurrentUserAccessor`, replaceable): the caller is
  a principal set by `RunAs`, else the Blazor authentication state, else the request's user, else nobody. The six
  services that read the user (Content, Media, Membership, Language, Seo, Tag) use it instead of
  `AuthenticationStateProvider`, which has a state only inside a Blazor render: a save from a controller, an API or
  a hosted service threw (ZA-093).
- **A write with nobody signed in is saved and records no user** (null `LastUpdatedById` / version `CreatedById`, no
  audit line). Content-type, media, sort/move and audit paths used `user!` and threw; the page save refused.
- **`RunAs`** holds per async flow (an `AsyncLocal`), not per scope, for the scope that set it and for scopes with
  no render of their own; a circuit reached through an AppState event keeps its own user. The installer runs the language and the starter
  kit as the new administrator with it, replacing its own work-around that set the scope's authentication state.
  The installer test now fails if that run-as is missing, which also closes ZA-152.
- Rule written down in `AGENTS.md`, [Services.md](Services.md) "Callers and access" and [AdminUI.md](AdminUI.md).
- Tests: `Membership/CurrentUserAccessorTests.cs` (9): the request's user outside a render, nobody instead of an
  exception, `RunAs` nesting, `RunAs` not leaking into another flow of the same scope, content / content-type /
  sort and media saves from a job with nobody signed in, a job that runs as a user, a `RunAs` that does not reach
  another signed-in scope, a request id that is not ours. Reviewed by a separate agent: its two Mediums (a foreign
  id threw, `RunAs` reached other circuits' event handlers) and Lows fixed.
- Findings: ZA-093 fixed, ZA-140 closed by D27, ZA-152 resolved.
- Release notes (Phase 10a):
  - **Breaking:** the constructors of `ContentService`, `MediaService`, `MembershipService`, `LanguageService`,
    `SeoService` and `TagService` take `ICurrentUserAccessor` instead of `AuthenticationStateProvider` (code that
    constructs or subclasses them recompiles).
  - Service writes work outside a Blazor circuit (controllers, minimal APIs, hosted services). With nobody signed
    in they record no user; wrap calls in `ICurrentUserAccessor.RunAs` to record one.
  - Services don't check who may write (unchanged, now documented): a site's own front-end code, endpoints and
    plugin pages check access before they call a write method. The page save no longer refuses with "Sign in to
    save content" when nobody is signed in (a refusal added earlier in the rework; 4.x threw there).
  - A starter kit runs inside `RunAs` for the new administrator. A kit that read the user from
    `AuthenticationStateProvider` itself worked because the installer set that state; it now throws there and
    should use `ICurrentUserAccessor` or `StarterKitContext.Administrator`.
  - A caller signed in by another authentication scheme whose id is not a ZauberCMS user id counts as nobody.

### Phase 10b — Save hooks and content data — DONE (2026-11-26)
ZA-012, ZA-018, ZA-077, ZA-099 (Medium); ZA-117, ZA-144, ZA-145, ZA-146, ZA-157, ZA-159 (Low), all fixed; ZA-162
(content type export always threw) found and fixed on the way. The owner delegated the two design questions ("an
enterprise CMS; decide as Umbraco does"): D28 and D29.
- **Save hooks follow the change tracker (D28).** `SaveChangesAndLog` (both overloads, one implementation) runs the
  hooks for every row the save writes, of every entity type, each with its own state; the named entity's hooks run
  first and even when it has no changes, as before. One veto and nothing is written. So sort, move, restore,
  recycle, permanent delete, content type saves (ZA-012), media binned, restored or deleted below an item and an
  emptied media bin (ZA-157), blocks deleted by Clear changes or rewritten by a version restore (ZA-159) all reach
  the hooks. The early audit saves that made hooks see `Unchanged` are gone: audit rows are staged and written by
  the change's own save, so a vetoed or failed change leaves none. Copy and content import (raw `SaveChangesAsync`)
  and version delete now go through `SaveChangesAndLog` too. The cache prefix of every type written is cleared.
- **Drafts (D29).** A "Save only" runs the `UnpublishedContent` hooks for the drafts it writes; `Content` hooks see
  only the rows it really changes (the page's link to its draft, a new block's row).
- **Tags (ZA-018, ZA-099).** `SaveTagItemAsync` writes the links and their audit rows in one save through the hooks.
  The tags editor no longer writes to the database (it did on every render, prerender included, before the page was
  saved): the page save writes an item's tag links from the values it publishes, through a new discovered
  `ITagReferenceProvider` (built-in `TagsPropertyTagReferenceProvider` for `ZauberCMS.Tags`). A draft leaves the
  links; a publish, version restore, copy or import writes them; a permanent delete (and a block removed by a save)
  removes them. A value naming a deleted tag is skipped. An item whose type has no tag property keeps links set by
  code.
- **Import (ZA-077).** Importing a content type over an existing alias keeps the ids of the tabs and properties it
  already has, matched by alias, so stored values stay theirs; the system tab keeps its fixed id; imported content
  records no user instead of `Guid.Empty`. Export (ZA-162) `Include`d the JSON tabs column and threw on every call.
- **Versions (ZA-146).** A version write locks the page's row (an update that changes nothing) and numbers the
  version inside its transaction, so two at once run one after the other; the old current-published / latest-draft
  flag is switched off as tracked rows (hooks see it) and saved before the new one. Version restore runs in one
  transaction from the flag switch to the save.
- ZA-117: `SaveUserAsync` / `CreateUpdateUserAsync` copy only the profile fields (new `UserMappingExtensions.MapProfileTo`:
  phone number, extended data; user name and email through `UserManager` as before). ZA-144: a restore that gives a
  page a new URL gives its pending draft that URL. ZA-145: a move loads the subtree once (tracked) for the loop check
  and the path rewrite; copy walks ids only.
- Tests: `SaveHookTests` (moves, recycle veto, content type veto, media bin and emptied bin with a veto, drafts,
  tag links; on all three databases), `ContentDataTests` (tag links, import, restore URL; all three),
  `ContentVersionRaceTests` (four versions at once; PostgreSQL and SQL Server), `UserSaveTests`. Test hooks are
  discovered for the whole test assembly and act only on the async flow of a test that starts a `SaveHookRecorder`.
- `/audit changed` (one reviewer): two Mediums fixed — a save passed the caller's copy (media) ran the hooks twice,
  once for the copy as Detached (the hooks now get the tracked row with that key, once); a block added in a "Save
  only" got no tag links when the page was published later (the publish now writes the links of every block it
  reaches). Lows fixed: removed values were still read for tags; `CreateVersionAsync` read the page before the lock
  (the lock now comes first, so the snapshot is serialised too); the `Tag` cache was not cleared by a link change
  (`TagItem` writes clear the `Tag` prefix); two tabs or properties with one alias got the same kept id on import;
  deleting a content type left its content's tag links. Noted: a version restore holds the page row's lock while it
  reads the block tree.
- Not browser-tested: the tags editor (it now only sets its value; the page save does the rest, covered by tests).
- **Release notes (D18):**
  - Save hooks run for more rows: every row a save writes, of every type, including the items below a moved,
    binned, restored or deleted item, drafts, blocks, tag links and audit rows. A hook that assumed it only sees the
    item named should check the row it is given. One `false` stops the whole change.
  - A concurrency conflict on a single-entity save comes back as the user-readable message, as the many-entity save
    already did.
  - Tag links are written by the page save from the published value, not by the tags editor as it renders. A "Save
    only" no longer changes them. Sites upgrading keep their links; the next publish of a page sets them from its
    value.
  - `SaveUserAsync` and `CreateUpdateUserAsync` no longer write the password hash, security and concurrency stamps,
    lockout, failed sign-ins, two-factor, email/phone confirmation or normalised names from the copy passed in;
    change those through `UserManager`.
  - Importing a content type over an existing alias keeps its property and tab ids (before, the values were lost).
  - The tags editor component no longer injects `NotificationService`.
- After-save hooks still run inside the change's transaction, before the commit (Umbraco raises its "-ed"
  notifications after the scope completes); see Phase 10f (ZA-163).

### Phase 10c — Property editors and stored values — DONE (2026-11-26)
ZA-015, ZA-078, ZA-100 (Medium); ZA-027, ZA-028, ZA-029, ZA-112, ZA-127 (Low). The design questions were decided
the Umbraco way (owner delegated): D30 (one picker shape, old values read, no data migration) and D31 (invariant
numbers and dates, unique aliases).
- **Pickers (ZA-078, ZA-029, D30).** `PickerValueExtensions` (`ToPickerIds`, `ToPickerValue`, `InPickerOrder`) is
  the one reader and writer. The content and media pickers write a JSON array for any count, load every picked item
  (the query default of 10 dropped the rest, and the next change saved the value without them), show unpublished
  pages, keep the stored order and save after a drag (a drag changed nothing). `GetContentItems`, `GetMediaItems`
  and `GetUsers` return the stored order and read both shapes; the one-item path now goes through the same cached
  query, so a picked page that is unpublished or in the recycle bin is no longer shown on the site.
  `GetValue<Guid>` reads `["id"]` and `GetValue<List<Guid>>` reads a bare id. The admin's single-id fields
  (internal redirect, content type image, SEO image, sitemap root) take either shape.
- **Aliases (ZA-028, D31).** `SaveContentTypeAsync` refuses two properties with one alias, ignoring case, on the
  type or between it and its compositions, from either side. `ContentValues()` (pages and users) and the version
  compare take the first value of a repeated alias instead of throwing.
- **Culture (ZA-027, D31).** The numeric editor writes `1.5`; the date editor writes `2026-11-26T14:30:00` and keeps
  the time (the long-date text dropped it, so the time-only picker could not work), and clearing it now clears the
  value. `ToValue<decimal|double|DateTime>` reads the invariant form first, then the current culture's; `Float`, so
  `1,5` is never read as 15. The rating editor no longer throws on a value that is not a number.
- **Uploads (ZA-100).** `StagedBrowserFile` copies each chosen file to the system temp folder
  (`ZauberCMS/uploads`) when it is chosen; Save uploads from the copies and deletes them, as do Remove, Clear and
  closing the editor. Save and Clear are disabled while files are being read; Remove takes the item clicked, not
  the first with its name.
- **Validation (ZA-015).** `AddFluentValidation` returns an `IDisposable` that unsubscribes and clears its messages;
  `FluentValidationValidator` disposes it and moves to a new `EditContext`. A validator that throws is logged and
  leaves the form invalid (`ValidatorFailedMessage`); the assembly scan is under a lock. A model with no validator
  now passes `ValidateAsync` instead of throwing "No pending ValidationResult found".
- **`SortableList` (ZA-112).** `OnUpdateJS`/`OnRemoveJS` return the callback's task; dispose always releases the
  `DotNetObjectReference`, and a module loaded after the list was disposed is disposed too.
- **Lifecycle (ZA-127).** Property editor rows in the content and user editors are keyed on the loaded copy, so a
  version restore, Clear Changes, a save elsewhere or moving to another user no longer leaves editors showing the
  old values (and saving them back). Also: the map editor builds its map once; the upload editor, the "Create"
  media buttons and the role's user list load when their id changes (a missing role lists nobody); the tags editor
  reads its value again when it changes; the SEO checker keeps the URL typed; the image crop and the language
  dialog keep the editor's choice when the side panel re-renders; `RenderBlock`, `PickerItem` and the pickers no
  longer write their own parameters; the settings list editor no longer crashes on two equal values (no value
  `@key`, remove by position, a repeated value isn't added); property rows in the content type and role editors
  are keyed. Left as it was: `EditContent` reloads the page on each parameter set (a save navigates to the same
  URL to refresh it).
- Review (`/audit changed`, one reviewer; nothing High). Fixed: picking or pasting files while the last pick was
  still being read could end the circuit (the input is disabled while busy, every per-file error is shown, and
  Remove waits for a save); `FluentValidationValidator` moved to a new subscription whenever `Validator` was a new
  instance (`Validator="@(new X())"`) and so cleared its own errors (now by type); `User.GetMedia`/`GetContent` kept
  query order; `GetValue<Guid?>`, `Guid[]` and `IEnumerable<Guid>` read only one shape; saving a composition was
  refused for a clash already stored inside a type that uses it (a clash counts only when the type being saved is
  part of it); a failed drag was an unhandled promise in the browser (logged on the server now); removing the
  sitemap's root page kept it; the SEO tags ignored a changed title, description or fallback image; the staging
  tests counted files in the shared temp folder. Noted only: a page whose values repeat an alias shows the first in
  load order.
- Tests: `Content/PropertyValueTests.cs` (picker order and old values, alias clashes, on all three providers; value
  formats, repeated aliases and FluentValidation without a database), Components `Editors/StagedBrowserFileTests.cs`.
  Full suite 897 (896 pass, 1 skipped for ZA-118) on SQLite, PostgreSQL, SQL Server LocalDB and Redis.
  Not tried in a browser: the picker drag, the two-batch upload and the date editor; the value formats and the
  service paths they use are covered by tests.

Release notes (Phase 10c):
- Pickers store one item as `["id"]`, not a bare id. Views using `GetContentItems`/`GetMediaItems`/`GetContent`/
  `GetMedia`/`GetUsers`, `GetValue<Guid>` or `GetValue<List<Guid>>` keep working; code that parsed the raw string
  with `Guid.Parse` must use `ToPickerIds()`. Existing values change shape the next time their page is saved.
- Front-end pickers return items in the order the editor chose, and only published pages not in the recycle bin
  (before, one picked page was returned even when unpublished or recycled).
- Numbers are stored as `1.5` and dates as `yyyy-MM-ddTHH:mm:ss`; values written before still read. A date read with
  `GetValue<DateTime>` now works (before it returned the default).
- A content type whose properties repeat an alias (ignoring case), on the type or through a composition, can no
  longer be saved; rename one of them.
- `AddFluentValidation` returns `IDisposable` (it returned nothing); `SortableList.OnUpdateJS`/`OnRemoveJS` return
  `Task`. New public helpers: `PickerValueExtensions`, `EnumerableExtensions.ToValuesByAlias`, `StagedBrowserFile`.
- Uploads are staged in the system temp folder; a host whose temp folder isn't writable can't upload from the admin.

### Phase 10d — Admin UI robustness — DONE (2026-11-27)
ZA-096, ZA-097, ZA-101 (Medium); ZA-025, ZA-085, ZA-108, ZA-156, ZA-161 (Low) fixed; ZA-026 was already fixed
by ZA-138 (Phase 4); ZA-160 not reproduced. The error-boundary question was decided the Umbraco way (owner
delegated): D32. The owner added the resizable tree panel during the phase.
- **Errors (ZA-097, D32).** New public `AdminErrorBoundary` (`ZauberCMS.Components.Shared`): "*Name* could not be
  shown", the exception's message only with Blazor's detailed errors on, and "Try again". Round each property editor
  (`DynamicContentProperty`, so the content, block and user editors), each dashboard tab, the sidebar navigation and
  the page body (`SectionLayout`, cleared on navigation). The vendored modal puts a plain `ErrorBoundary` round each
  panel's content, with "Try again" and "Close" (`Core/Modal/README.md` lists the change); one round
  `<RadzenComponents />` catches Radzen dialogs and side panels (an error closes them; a box over the page says so).
  Block previews already
  render apart and catch their own errors. Front-end views have no boundary (Phase 10e, ZA-098).
- **No prerender (ZA-101).** The admin's `Routes` and `HeadOutlet` use `InteractiveServerRenderMode(prerender: false)`;
  `RadzenTheme` still prerenders its stylesheet link.
- **Full page loads into the admin (ZA-096).** Reproduced the mechanism, not the original failure: blazor.web.js
  (10.0.15) retries an enhanced navigation as a full page load when a successful response lacks
  `blazor-enhanced-nav: allow`, so `AdminAccessMiddleware` removes that header from every `/admin` response. That
  covers any link, redirect or form from a static page (a site's own views included); `NewSite.razor`'s link also has
  `data-enhance-nav="false"`.
- **Scripts (ZA-108).** Sortable.js 1.15.7 (npm tarball, integrity checked against the registry) ships in
  `ZauberCMS.Components/wwwroot/lib/sortablejs` with its MIT licence; it was 1.15.4 from cdnjs without `integrity`.
  `blazor.web.js`, Radzen, `block-preview.js` and the editors' `Scripts`/`Styles` go through `@Assets[...]`
  (fingerprinted; a path that is not a static web asset is unchanged).
- **SEO checker (ZA-085).** The synchronous HTTP was already gone (`SeoHttp` has timeouts). A run is now cancelled
  when the editor leaves, runs again, presses the new Stop button, or after 3 minutes; a check that throws shows as a
  failed check (logged) instead of ending the circuit; results show as each check finishes. `ISeoCheck` gets a
  `Check(…, CancellationToken)` default method (existing checks keep working); `BrokenLinksCheck` and
  `ResponseStatusCheck` implement it.
- **Recycle bins (ZA-156).** `ContentTreeLevelParameters.InRecycleBin` / `MediaTreeLevelParameters.InRecycleBin`:
  the top of the bin (items whose parent is not binned) a page at a time, most recently binned first, with child
  flags; with `ParentId`, what is in the bin below that item. Both bins use them: 100 at a time with "Show more" (the
  media bin stopped at 500), and a binned page or folder opens. Restoring an item inside a binned parent is refused
  by the services, as before; a permanent delete takes it and what is below it.
- **"Show more" (ZA-161).** The row is a button inside the node template (`TreeExtensions` template overloads taking
  the tree and its load method) with `onclick:stopPropagation`: Radzen never selects it, so the selected node keeps
  its highlight. The selection path stays for the keyboard.
- **ZA-025.** `Constants.Sections.Trees.StructureElementTypeTree` is `"StructureElementTypeTree"` (it was
  `"StructureContentTypeTree"`, the content type tree's alias).
- **ZA-160.** Not reproduced: four full reloads of a media folder page with 105 items, after this phase's changes,
  logged nothing. Our only component on that page that calls JavaScript on dispose (`MultipleMediaUpload`) catches
  the disconnect; the rest are Radzen's. Reopen with a stack trace if it shows again.
- **Resizable tree panel (owner request, 2026-11-27).** A divider on the sidebar's edge (`role="separator"`,
  focusable) sets Radzen's `--rz-sidebar-width` on `<html>`: drag, or arrow keys (Shift for steps of 64px), Home,
  End; Enter or a double-click goes back to the theme's 300px. 200px to 60% of the window, at most 720px; kept per
  browser in `localStorage` and applied by a head script before the admin paints, so there is no jump. Hidden below
  768px and when the sidebar is collapsed. The trees now scroll inside the sidebar (`.zauber-sidebar-scroll`), so the
  divider stays in place. Script: `ZauberCMS.Components/wwwroot/js/admin-sidebar.js`. As in Umbraco, the width is
  kept per browser, not per user.
- Checked in a browser on a scratch copy of the sample site (a known password set on the copy, 106 binned media, a
  binned folder with a subfolder, a binned page with two pages below, a live folder of 105 files): a link from the
  home page into `/admin/media` loads as a full page with Radzen and Sortable present and every script fingerprinted;
  `/admin` HTML has no prerendered layout; the media bin's "Show 6 more" and binned folder, the content bin's binned
  page and the refused restore of a child; "Show 5 more" in the live folder keeps the selected file highlighted; a
  dashboard that throws shows the boundary with the message (Development), "Try again" brings it back and the
  circuit carries on; the divider's drag, clamp, reload, keyboard, reset and collapse. Not tried in a browser: a
  side panel that throws (same `ErrorBoundary`, tested only through `AdminErrorBoundary`), the SEO Stop button
  (`SeoHttp` refuses the local site, so a run cannot be started on it).
- Review (`/audit changed`, two reviewers; nothing High). Fixed: Radzen dialogs had no boundary (Medium); content
  "Delete All" stopped at 1000 rows and counted blocks (it now pages the top of the bin through the tree level);
  a site's `ISeoCheck` without the token overload could not be stopped (`WaitAsync`); a Stop after the run ended
  stuck; the "Show more" button was a Tab stop inside the tree (`tabindex=-1`, the arrow keys reach the row); a
  drag of the divider could outlive its pointer; a keyboard "Show more" in a bin cleared the selection; the docs
  left out that `Zauber:ShowDetailedErrors` is ignored in Production. Noted, not changed: any content or media save
  (any circuit) reloads the bins and drops pages loaded with "Show more", as the bins did before.
- Tests: `Shared/AdminErrorBoundaryTests` (Components: the error replaces the child and is logged, the message only
  with detailed errors), `Shared/QueryAndCacheTests` ZA156 (both bins, all three databases), `Security/AdminAccessTests`
  ZA096, `Seo/SeoCheckTests` (stopped broken-links and response-status checks stop, a running one still reports, old
  checks run through the new method), `Shared/ConstantsTests` (tree aliases distinct). The bin tests also show that
  binned blocks are never listed (the content bin listed them before).
  Full suite 912 (911 pass, 1 skipped for ZA-118) on SQLite, PostgreSQL, SQL Server LocalDB and Redis; Release build
  0 warnings; no vulnerable packages.
- **Release notes (D18):**
  - The admin is not prerendered: an admin page initialises once, in the circuit, with no `HttpContext`. A plugin
    admin page that read `HttpContext` during prerender gets null.
  - Every `/admin` response refuses enhanced navigation (no `blazor-enhanced-nav` header): links into the admin
    from static pages load a full page; the browser logs a warning when it falls back.
  - An exception in a property editor, dashboard, the navigation, a page or a side panel shows an error with "Try
    again" instead of ending the circuit. New public component `AdminErrorBoundary`.
  - Sortable.js is served from the Components package (1.15.7), not cdnjs. Admin scripts and editor assets are
    fingerprinted through `@Assets`.
  - `ISeoCheck` has a new `Check(url, document, content, CancellationToken)` default method; implement it in a check
    that makes requests so it can be stopped. The SEO checker stops a run after 3 minutes.
  - New `InRecycleBin` on `ContentTreeLevelParameters` and `MediaTreeLevelParameters`. Both bins page by 100,
    newest binned first (the content bin listed oldest first), and binned folders open. The content bin no longer
    lists binned blocks; "Delete All" deletes everything in the bin (it stopped at 1000 rows). The content bin's "Show
    more" row is a `TreeLoadMore`, no longer a `TreeBranch` (a bin context menu that matched every `TreeBranch` no
    longer shows on it).
  - **Breaking:** `Constants.Sections.Trees.StructureElementTypeTree` is now `"StructureElementTypeTree"`. A context
    menu or tree that used only `StructureContentTypeTree` no longer shows on the element types tree; add
    `StructureElementTypeTree` to its `TreeAlias`.
  - New `TreeExtensions.CreateStandardTreeTemplate/CreateContentTreeTemplate/CreateMediaTreeTemplate(receiver,
    loadMore)` overloads (the parameterless ones are unchanged).
  - The admin sidebar can be resized (per browser). The sidebar's content scrolls inside `.zauber-sidebar-scroll`; CSS
    that styled `.rz-sidebar` as the scroll container may need the new element.

### Phase 10e — Front end, security and identity hardening — DONE (2026-11-27)
ZA-095, ZA-098 (Medium); ZA-030, ZA-031, ZA-032, ZA-033, ZA-104, ZA-105, ZA-106, ZA-107, ZA-110, ZA-111, ZA-114,
ZA-115, ZA-116, ZA-149, ZA-150 (Low) fixed. The owner answered the four "ask first" items with the recommended
option (2026-11-27): D33 (error page), D34 (redirect regex flag), D35 (deleting a user erases them), D36 (image
defaults). New: ZA-164 (the front end's 404 page has an empty body; there before this phase), ZA-165 (a redirect's
domain is ignored).
- **Paths (ZA-095).** `SitePaths.IsSystemPath` (Core, `ZauberCMS.Core.Middleware`) matches `/admin`, `/account`,
  `/api` and `/_blazor` on whole segments; the culture and redirect middleware use it, and `App.razor` uses
  `AdminAccess.IsAdminPath`. Before, `/administration` rendered as the admin (checked: 200 with the admin on the
  commit before, a 404 now) and `/accounting` or `/api-docs` skipped redirects and culture.
- **Error page (ZA-098, D33).** Outside Development, `AddZauberCms` puts `ErrorPageMiddleware` and ASP.NET's
  exception handler (`Zauber:ErrorPagePath`, default `/error`) right after the forwarded headers. A content page at
  `/error` is the site's error page; with none, or one that fails too, the built-in page (no database, no layout,
  the request id, `no-store`) is sent with 500. During that re-execution `EntryPage` throws when there is no page
  (rendering even a 404 would write the layout and count as the error page), the error page is requested as a GET
  (a failed form post is not posted to it), and the installer and redirect rules let it through. The two
  `Program.cs` files lose their dead `/Home/Error` handler. Found on the way: `CultureMiddleware` and
  `RedirectMiddleware` called `next` inside their own `try`, so a page that threw was caught, logged as their error,
  and **run a second time** (its side effects twice; the second run then failed on a duplicate header and hid the
  real exception). Both now do only their own work inside the `try`.
- **Restricted pages (ZA-110).** `EntryPage` decides access before choosing the view and returns after the redirect,
  so nothing of the page renders even where `NavigateTo` does not end the render.
- **Culture (ZA-114).** `BlankLayout` uses `AdminCulture.Apply` (this render's culture only); the process-wide
  defaults were already gone. The sitemap keeps the root when its internal redirect is `Guid.Empty` or points at a
  missing page (the home page was left out).
- **Cancellation (ZA-111).** `DailyJob` is a `BackgroundService` with a `PeriodicTimer` and passes the token to its
  three services; the sitemap generator passes it through and writes a temporary file that replaces the sitemap
  when complete; the culture and redirect middleware pass `RequestAborted` and stop quietly when the visitor has gone.
- **Performance (ZA-115).** Generated regexes in `StringExtensions` (one was compiled on every call); grid paging
  (`GetDataGridAsync`) and content queries end with `ThenBy(Id)`, the root and slug lookups are ordered; a unique URL
  takes one query instead of one per `-1`, `-2`…; the redirect list lives in `ICacheService` under `SeoRedirect`, so
  a save clears it on every server (it was per process); `DefaultCacheService` shares one load between requests that
  miss the same key together (the loads under way are kept on the singleton `ServerCache`; a request after a clear
  never joins a load started before it).
- **Redirects (ZA-116, D34).** `SeoRedirect.IsRegex` (`bool?`, migrations `RedirectIsRegex` ×3): null for old rows,
  which keep the old guess (`RedirectRules.LooksLikeRegex`) until saved. The dialog has a "Regular expression?"
  switch (set from what a row is matched as today) and checks the From URL; the list shows Exact/Regex. A save is
  refused for a From URL that cannot match, a redirect to itself, or one that closes a circle with the saved ones
  (`RedirectRules.FindLoop`, up to 20 steps, trying rules in the middleware's order: exact before regex, newest
  first); the middleware never redirects an address to itself. Exact matches ignore letter case; of two with the
  same From URL the newest wins. Also fixed in the dialog: editing showed "Any" as the domain, and the panel title
  said "Add Redirect" when editing.
- **Image defaults (ZA-104, D36).** `AllowUpscale` false and `Cache:MaxCacheBytes` 2 GB (the package enforces the
  cap as it writes); the Web and template settings files have the new values.
- **External sign-in (ZA-105).** `IMembershipService.ExternalLoginAsync` no longer creates an account itself: a first
  sign-in goes through `ZauberSignInManager.CreateExternalUserAsync` and its checks; when that refuses, the call
  fails. A refused existing account (`IsNotAllowed`) gets its own message.
- **Unconfirmed accounts (ZA-106).** Identity refuses an unconfirmed account before it checks the password, so the
  login now checks the lockout and then the password: a locked account is refused as locked, a wrong password is
  "Invalid login credentials" and counts towards lockout, only the right one hears "Email isn't confirmed". The
  confirmation email goes again at most every 10 minutes per account (`MembershipService.ConfirmationResendInterval`,
  per server); a send that fails is logged and does not count.
- **Logs (ZA-107).** No email address in any log line: the lockout warning logs the user id, the error lines none.
- **Sign-out (ZA-030).** `GET /api/auth/logout` submits itself only when the browser says the navigation came from
  this site or was typed in (`Sec-Fetch-Site` same-origin/none; without it, a Referer on this host); anything else
  shows a "Sign out" button, and the page cannot be framed (`X-Frame-Options`, `frame-ancestors`). The POST and its
  antiforgery check are unchanged.
- **MD5 (ZA-031).** The generated user name for an external sign-in hashes with SHA-256 (new names differ from
  before; existing ones stay). `StringExtensions.ToMd5` and `ClaimsPrincipalExtensions.GetMd5` are `[Obsolete]`; the
  `Md5Hash` claim is still issued until the next major version.
- **Settings (ZA-032, ZA-149).** The shipped settings have empty email values (they had `mail_username`,
  `mail_password` and a SendGrid host); `SmtpEmailProvider` says which key is missing and signs in to the SMTP server
  only when a user name is set. `Zauber:Identity:LoginRateLimit` / `RegisterRateLimit` (`PermitLimit`,
  `WindowMinutes`; 0 turns a limit off) replace the numbers fixed in code, with the same defaults.
- **Form tokens (ZA-150).** A token carries a random id; `IFormProtectionService.TryUseToken` (a default interface
  method, so replacements keep compiling) marks it used once a form passes, and `Validate` then returns the new
  `FormProtectionResult.AlreadyUsed`. The registration page uses it and hands out a new token after a refused post.
  Used tokens are kept in each server's memory until they expire (12 hours), by what they decrypt to (the same token
  with spaces added is the same token).
- **Deleting a user (ZA-033, D35).** `DeleteUserAsync` erases the person in one `SaveChangesAndLog` (`UserAnonymiser`):
  user name `deleted-<id>`, no email, phone or password, profile values, logins, tokens, claims and roles removed, a
  new security stamp; the row stays, marked deleted, for the pages and versions it wrote (if someone registered that
  name first, a random part is added). The address and the name can register again. Refused: deleting your own
  account and the last administrator; the dialog says it cannot be undone. The audit row is written after the save.
  `RecoverAdminUser` refuses an erased user (it still restores one deleted before this). Users deleted before this
  keep their data; the owner chose not to erase them on upgrade (D35).
- Checked in a browser on a scratch copy of the sample site, in Production mode: a page that throws gets the
  built-in page (500) for a GET and for a form post, and once the copy has a content page at `/error`, that page
  with 500 for both, and the page's own exception in the log; `/administration` is an ordinary 404; the sign-out page
  with `Sec-Fetch-Site: cross-site` shows the button and with `same-origin` submits itself; the redirect dialog's new
  switch, an exact `/old-search?q=1` redirect (301 to `/blog`, also in capitals; `/old-searc?q=1` not redirected) and
  the refused loop with its message; registering, then a second visitor sending the used token ("This form was sent
  already", a new token, the next send works); deleting a user in the admin (the row erased, the audit row without
  personal data). The empty 404 body was compared on the commit before (same, so ZA-164 is older than this phase).
  Not tried in a browser: a first external sign-in, the 10-minute resend and the lockout of an unconfirmed account
  (tests only), the daily job.
- Review (`/audit changed`, two reviewers). Fixed: an unconfirmed account could be password-guessed without lockout
  (High: Identity reports "not allowed" before the lockout, and the new password check ignored it); the single-load
  cache only shared loads within one request (High: it lived on the scoped service); with no page at `/error` the 404
  rendered and counted as the error page (High); a used form token with spaces added counted as new; deleting
  yourself or the last administrator was allowed, and taking the name `deleted-<id>` first stopped an erasure; the
  loop check tried rules in another order than the middleware; the installer or a broad regex redirect could replace
  the error page; a failed form post was posted to the error page; a stopped sitemap run left a cut-off file; the
  confirmation resend counted a send that failed; the sign-out fallback accepted requests with no Referer, and its
  page could be framed; `/_blazor` was not a system path. Noted, not changed: each regex redirect may take up to
  100 ms on every request when its pattern backtracks (`NonBacktracking` would refuse some patterns); the resend
  throttle and used tokens are per server; users deleted before this keep their data (D35).
- Tests: `Seo/RedirectAndCultureTests` (ZA116 exact and regex matching, old rows, refused loops in the middleware's
  order and refused From URLs; ZA095 segment matching and redirects on `/administration`; ZA098 a throwing page runs
  once and its exception reaches the handler, and the error page is never redirected), `Bootstrap/ErrorPageTests`
  (the site's page, the built-in page when there is none or it fails, settings), `Bootstrap/HardeningDefaultsTests`
  (image and rate-limit defaults and binding, no made-up email values in the three settings files),
  `Security/FormProtectionTests` ZA150 (one use, padded tokens, old tokens), `Membership/DeleteUserTests` ZA033 (the
  erasure, your own account, the last administrator, a taken name; all three databases), `Membership/RecoverAdminTests`
  (an erased user is not recovered), `Membership/UnconfirmedSignInTests` (wrong and right password, the resend once,
  lockout), `Shared/CacheSingleLoadTests` (one load, also across two requests' services), Components
  `Shared/LogoutTests`. `Shared/CacheTests`' ZA055 race test now drives `ServerCache` directly (two loads of one key
  no longer overlap). Full suite 993 (992 pass, 1 skipped for ZA-118) on SQLite, PostgreSQL, SQL Server LocalDB and Redis; Release build
  0 warnings; no vulnerable packages.
- **Release notes (D18):**
  - Outside Development an unhandled exception now shows an error page: `Zauber:ErrorPagePath` (default `/error`; a
    content page there is used), else a built-in page. A site that calls `UseExceptionHandler` itself sets the key to
    "" to keep its own. A page that throws is no longer run a second time by the culture and redirect middleware.
  - `/administration`, `/accounting`, `/api-docs` and similar are pages of the site (they were treated as admin,
    account or API paths). `/_blazor` is a system path: redirect rules and content culture leave it alone.
  - Redirects: new `SeoRedirect.IsRegex` (schema: one nullable column, three migrations). A redirect saved from now on
    is exact unless marked as a regular expression (also one created in code); old ones keep matching as before until
    saved. Loops and From URLs that cannot match are refused on save. The compiled list is cleared on every server
    when a redirect changes. The middleware's constructor no longer takes `IMemoryCache` and `InvokeAsync` takes
    `ICacheService`.
  - Image resizing: `AllowUpscale` now defaults to false and `Cache:MaxCacheBytes` to 2 GB. Set them back in
    `Zauber:ImageResize` to keep the old behaviour.
  - Deleting a user erases them (user name, email, phone, password, profile values, logins, claims, roles); there is
    no restore, and `RecoverAdminUser` cannot bring them back. Deleting your own account or the last administrator
    is refused. Deleting is not a ban; lock the account to keep someone out.
  - `ExternalLoginAsync` no longer creates an account when the sign-in manager refused to.
  - Signing in to an unconfirmed account: a locked one is refused as locked, a wrong password says "Invalid login
    credentials" and counts towards lockout; the confirmation email is re-sent at most every 10 minutes.
  - `GET /api/auth/logout` from another site (or without `Sec-Fetch-Site` and a Referer on the site) shows a button
    instead of signing out; a single sign-on portal that links to it from another domain gets the button.
  - Rate limits: `Zauber:Identity:LoginRateLimit` and `RegisterRateLimit`, same defaults.
  - Form protection: tokens are single-use. New `IFormProtectionService.TryUseToken` (default: remembers nothing),
    `FormProtectionResult.AlreadyUsed` (code that accepted a form when the result was not `Expired` must check for
    `Passed`), and a fourth optional `FormProtectionService` constructor parameter (`IMemoryCache`). A site's own form
    that uses the service should call `TryUseToken` once the form is accepted.
  - The shipped `appsettings.json` files have empty email settings. `SmtpEmailProvider` no longer takes
    `IServiceProvider`, and skips the SMTP sign-in when `Zauber:Email:Smtp:Username` is empty.
  - `ToMd5` and `GetMd5` are obsolete; generated user names for new external sign-ins use SHA-256.
  - `DailyJob` is a `BackgroundService` (still registered by `AddZauberCms`).
  - Content queries and data grids add `ThenBy(Id)` to their order; of two pages with the same URL (only possible in
    old data) the first in tree order is served.

### Phase 10f — Hooks after the commit, resized copies of deleted images — DONE 2026-11-27
ZA-163, ZA-155 (Low). Both were in the Backlog, which the owner removed (2026-11-27). Two parts: 10f-1 (hooks and
deletes: ZA-163, ZA-155, ZA-193) and 10f-2 (front-end cache and domains: ZA-187, ZA-189, ZA-190), both done below.
- **ZA-163:** `IAfterEntitySave<T>` runs inside the change's transaction, before the commit, so a change rolled back
  after it (a media purge whose file can't be removed) was already reported as done. Umbraco raises its "-ed"
  notifications when the scope completes. Add the after-commit point the Umbraco way, keep `IAfterEntitySave<T>`'s
  current contract working (it can ask for a re-save inside the transaction), and record the design as a D-decision.
- **ZA-155:** a permanent media delete leaves the image's resized copies in `_mediacache`. Remove them, or prune the
  cache, whichever the ImageResize package allows; decide it the Umbraco way and record it.
- From the Phase 11 audit (front-end cache and domains):
  - **ZA-187 (Medium):** the URL→page cache key holds the raw scheme, host, port and path, so every Host header
    value, port or spelling of a path (`/about/`, `/about//`) caches the same page again (bounded only by the
    entries' lifetime). Key it on what decides the answer: the matched domain, the normalised slug and the flags.
  - **ZA-193:** deleting a domain deletes its redirects by cascade in the database: the `SeoRedirect` cache is not
    cleared and the save hooks never see those deletes. Delete them through the change (with the hooks), as the
    after-commit work here touches the same path.
  - **ZA-189:** `RedirectMiddleware` compiles only the newest 5000 redirects and ignores older ones silently.
  - **ZA-190:** domains are matched against the path with `PathBase` in a circuit and `EntryPage`, without it in the
    middlewares; a site under a path base gets different domains on the two sides.

#### Phase 10f-1 — Hooks after the commit, resized copies, domain deletes — DONE (2026-11-27)
ZA-163, ZA-155, ZA-193 (Low) fixed. Decisions D62 and D63 (delegated, Umbraco's way). ZA-187, ZA-189 and ZA-190 are
part 10f-2.
- **After-commit hook (ZA-163, D62).** New discovered `IAfterEntityCommit<T>` (`ZauberCMS.Core.Shared.Interfaces`,
  `AfterCommitAsync(entity, state, cancellationToken)`, `SortOrder`), listed in `ExtensionManager.DiscoveryInterfaces`
  and AGENTS.md. `SaveChangesAndLog` builds its calls after the save, for every row the change wrote, with the state
  saved; with no transaction open it runs them after the after-save hooks (and their second save, even if that one
  fails), otherwise it leaves them with `AfterCommitHooks`, an EF Core `DbTransactionInterceptor` the three DbContexts
  add in `OnConfiguring`: it runs them when that transaction commits (a synchronous `Commit()` too) and drops them on
  a rollback, a failed commit, or a transaction disposed without either. A hook that throws is logged; the others run
  and the result stays a success. `IAfterEntitySave<T>` is unchanged (inside the transaction, can ask for a second
  save); its documentation now says so and points at the new hook. A site's own `IZauberDbContext` without the
  interceptor gets the hooks at the save, with one warning.
- **Resized copies (ZA-155, D63).** `ResizedImageCache` (internal) wraps the registered `IImageCache` for the resizer
  only (`IImageResizerService` is registered again around it in `AddZauberCms`), so each image's copies sit in
  `{CacheRoot}/{shards}/{hash of its path}/`; ImageResize's own `IImageCache` stays registered, so its start-up prune
  and size limit work as before. `DiskStorageProvider.DeleteFile` removes the image's folder after the file (also when
  the file was already gone), and `SaveFile` over a file of the same name removes the old copies;
  `DiskStorageProvider` takes an optional `IOptions<ImageResizeOptions>` as its last constructor parameter.
- **Domain deletes (ZA-193).** `DeleteDomainAsync` loads the domain's redirects tracked and deletes them in the same
  change, with an audit row each, so the save hooks (before, after, after-commit) see them, one veto keeps the domain
  and every redirect, and the `SeoRedirect` cache (the compiled redirects) is cleared.
- Tests (30 new cases): `Shared/AfterCommitHookTests` (8, on all three databases: a save without a transaction; two
  saves in one transaction wait for the commit; a rollback and a disposed transaction report nothing and a later save
  only itself; a synchronous commit; a media purge whose file can't be removed (a test storage provider whose delete
  throws, as a locked file does on Windows) rolled back and unreported while the after-save hooks saw it; a purge that
  commits reports every row; a throwing hook; discovery), `Seo/RedirectDomainTests` ZA193 ×2 (the redirects deleted
  through the hooks and the cache cleared, another domain's and the global one kept; a veto on a redirect keeps
  everything), `Media/ResizedCopiesTests` (4, through ImageResize's real resizer: copies in the image's folder, the
  registered `IImageCache` still ImageResize's; a purge removes the image's copies and not another's, binning keeps
  them; a file written over one of the same name; nothing to remove). Each was seen failing with its fix reverted
  (the controls pass both ways). `SaveHookRecorder` records the after-commit hook. `IAfterEntityCommit`1` added to the
  public types snapshot. Full suite 1205 (all pass, none skipped) on SQLite, PostgreSQL, SQL Server LocalDB and
  Redis; Release build 0 warnings; the template smoke test passes (Windows PowerShell 5.1). Dev site
  (`ZauberCMS.Web`, Release, Production): `/` 200, a `/media` image with
  `?width=40` resized (1878 of 610998 bytes) and its copy written to `_mediacache/f3/fc/ae60…/` (the hash of its
  path), the original 200.
- **Not verified:** a permanent delete from the admin in a browser (the dev site has no administrator account; the
  service path is covered by the tests with the real resizer); a web farm; a site's own DbContext without the
  interceptor (the fallback is a code path with a warning, no test).
- **Release notes (D18):**
  - New: `IAfterEntityCommit<T>`, a save hook that runs once the change is committed and never for one rolled back
    (Docs/Services.md "Write path"). Move work outside the database (mirroring files, notifying another system,
    indexing, mail) from `IAfterEntitySave<T>` to it; `IAfterEntitySave<T>` behaves as before.
  - A permanent media delete removes the image's resized copies from the image cache, and a file written over one of
    the same name removes the old copies. The cache is laid out per image from this version: copies made before the
    upgrade are not removed by a delete and are not used any more; delete the cache folder (`_mediacache` by
    default) once after upgrading to reclaim the space.
  - Deleting a domain runs the save hooks for its redirects, writes an audit row for each, and a veto on one keeps
    the domain; the redirects stop applying at once (before, the compiled redirects stayed cached).
  - `DiskStorageProvider` has a new optional constructor parameter (`IOptions<ImageResizeOptions>`, last); a subclass
    compiles unchanged, and without it the copies are not removed.
  - Breaking: none.

#### Phase 10f-2 — The URL lookup cache, domains under a path base, every redirect — DONE (2026-11-27)
ZA-187 (Medium), ZA-189, ZA-190 (Low) fixed. Decisions D64 and D65 (delegated, Umbraco's way). This closes Phase 10f.
- **URL lookup cache (ZA-187, D64).** `GetContentFromRequestAsync` works out the domain first (the cached domain
  list, no database on a hit) and keys the lookup on the time window, that domain's id (none for a host on no
  domain), the normalised slug and the flags (`ContentService.EntryCacheKey`); before, the key hashed the whole
  parameters, the scheme, host, port and path of the address among them. Every Host header value, port and spelling
  of one page now shares one entry per domain, and every host on no domain shares one, so a visitor cannot grow the
  cache by making addresses up; misses are still not kept. The matched domain is passed to the lookup and the
  content finders instead of being matched again.
- **Domains under a path base (ZA-190, D64).** The middlewares matched a domain's path against `HttpRequest.Path`
  (without `PathBase`), the circuit's culture handler and `EntryPage`'s fallback against `NavigationManager.Uri`
  (with it), so under `/site` a domain `example.com/fr` was matched by the page request and not by the circuit. New
  public extension `NavigationManager.GetContentRequestUrl(uri = null)` (`ZauberCMS.Core.Extensions`): scheme, host
  and port, the path below the base address, the query string. `ContentCultureCircuitHandler` and `EntryPage` use
  it; `GetContentFromRequestParameters.Url` and `ContentFinderContext.Url` are documented as the address without
  the path base.
- **Every redirect (ZA-189, D65).** `RedirectMiddleware` asks for every redirect (`Amount = int.MaxValue`); the
  query's default page of 5000 (newest first) had left older ones out without a word. `QueryRedirectsParameters`
  keeps its default for other callers.
- Tests (10 new cases): `QueryAndCacheTests` ZA187 ×2 (five hosts, ports and spellings of a page on no domain: one
  entry; a domain's ports and host spellings one entry, the hosts on no domain another), on SQLite, PostgreSQL and
  SQL Server; `ContentCultureTests` ZA190 (a domain with a path under the path base `/site`: the page request and the
  circuit both get the domain's language) and the helper ×5; `RedirectDomainTests` ZA189 (5002 redirects: the oldest
  exact one and the oldest regex both fire); `ProviderQueryTests` ZA189 (the uncapped query on each provider).
  `ServerCache.Keys` (internal) lets a test count entries. The ZA187, ZA189 (middleware) and ZA190 (circuit) tests
  were seen failing with their fix reverted; the helper and provider tests are controls. Full suite 1221 (all pass,
  none skipped) on SQLite, PostgreSQL, SQL Server LocalDB and Redis; Release build 0 warnings; no start-up or
  packaging change, so the template smoke test was not run. Dev site (`ZauberCMS.Web`, Release, Production): `/` 200
  with the Host headers `localhost:5099`, `made-up.test`, `evil.test:1234` and `LOCALHOST`; `/about`, `/about/`,
  `/About`, `/about//` and `/about?utm=1` 200 on made-up hosts; `/no-such-page-xyz` 404.
- **Not verified:** a site really run under a path base (`UsePathBase`) with a domain that has a path, in a browser
  (the circuit handler and the middleware are covered by the tests, `EntryPage`'s fallback only by reading: it
  calls the same helper and runs only when `CultureMiddleware` did not look the page up); memory with tens of
  thousands of redirects (not measured).
- **Release notes (D18):**
  - The URL → page cache holds one entry per page and domain, whatever host, port or spelling of the address a
    visitor uses. Nothing to do.
  - Under a path base, interactive components and `EntryPage` match a domain with a path as the page request does
    (before, a circuit never matched one and ran in the page's own language). `GetContentFromRequestParameters.Url`
    is the address without the path base: site code that passed `NavigationManager.Uri` should pass the new
    `NavigationManager.GetContentRequestUrl()`. Sites without a path base see no change.
  - Every saved redirect applies. A site with more than 5000 redirects will find its oldest ones working again
    after the upgrade; delete the ones that should not.
  - Breaking: none.

### Phase 10g — Back-office roles: section access per role (D38)
The feature the owner chose (2026-11-27); it replaces the Backlog row "Back-office roles other than Admin".
- Which roles may open each section, set on an admin screen and stored as data; Admin always all; a seeded "Editor"
  role with Content and Media; plugin sections (`ISection`) take part with no extra registration.
- Gate the section navigation, the pages under `/admin` (`AdminAccessMiddleware` and `Routes.razor`, today
  Admin-only through `Components/Admin/_Imports.razor`) and the endpoints the admin calls. Services stay
  unchecked (D27).
- Docs: what a plugin section or page has to do (AGENTS.md "Core principle"), Installer.md, AdminUI.md.
- From the Phase 11 audit: **ZA-188**, a role-restricted page whose view is missing is rendered through
  `MissingView` without the role check (a site's own `MissingView` gets the whole page).

### Phase 10h — Admin localisation, English resources (D37)
ZA-129 (Low). Every admin string through `IStringLocalizer` with English resource files; a site adds a language
with its own resources. Settle how a site supplies them (resource assembly or files) and record it. Done after 10g so
its new screens are covered.

### Phase 10i — Accessibility pass
ZA-128 (Low). Labels on icon buttons and inputs, keyboard access and roles for the clickable divs (every tree
button), `label for` matching real ids, focus in side panels. Last of the Phase 10 parts, so it covers 10g's screens
and uses 10h's localised text.
- From the Phase 11 audit: **ZA-194**, with registration off the registration page shows the not-found view inside
  the account layout, not the not-found view's own layout; **ZA-197**, the vendored `BlazoredModal` subscribes to
  three events in `OnInitialized` and never unsubscribes (harmless while it lives as long as the circuit).

### Phase 11 — Engineering baseline, API clean-up, v5 — DONE 2026-11-27
ZA-089, ZA-087, ZA-094, ZA-090, ZA-091, ZA-092, ZA-164 (Medium); ZA-109, ZA-118, ZA-119, ZA-120, ZA-121, ZA-123,
ZA-124, ZA-125, ZA-126, ZA-131, ZA-165 (Low).

The owner asked for Phase 11 before 10f–10i (2026-11-27); 10f–10i follow it and work to the conventions it sets.
Done in parts, one commit each, in this order:

| Part | Scope | Status |
| --- | --- | --- |
| 11a | ZA-164, ZA-165 (found in Phase 10e) | DONE 2026-11-27 (D41, D42; ZA-166 found and fixed) |
| 11b | Baseline: ZA-131, CI and release hardening ZA-089, ZA-109 | DONE 2026-11-27 (D43, D44) |
| 11c | Packaging and template: ZA-087, ZA-094, ZA-118, ZA-119, ZA-123, template smoke test | DONE 2026-11-27 (D45–D47; ZA-168 found and fixed, ZA-167 found, to 11d) |
| 11d | Start-up and DI: ZA-120, ZA-121, ZA-167 (found in 11c) | DONE 2026-11-27 (D48–D50; the 11c CI failure fixed) |
| 11e | Breaking: remove the unwired MVC path (ZA-090), Radzen/Modal-typed contracts out of Core (ZA-091) | DONE 2026-11-27 (D51, D52) |
| 11f | Breaking: internalise non-contract types (ZA-092), naming conventions (ZA-124) | DONE 2026-11-27 (D53, D54) |
| 11g | Content finders (owner request, 2026-11-27: D55); breaking: split the large services, remove `Content.Url` (ZA-125), custom components get an id or DTO (ZA-126), `IDbContextFactory` decision | DONE 2026-11-27 (D55–D59) |
| audit | `/audit changed` over 2f4dc41..773505c | DONE 2026-11-27 (ZA-169…ZA-186 fixed; ZA-187…ZA-198 to 10f, 10g, 10i, 12) |
| audit follow-ups | The owner's answers to the audit's questions: ZA-185, ZA-198 | DONE 2026-11-27 (D60, D61) |

- **Found in Phase 10e, do first (done in 11a, below):** ZA-164 (the front end's 404 page is sent with an empty body; reproduce in a bare
  .NET 10 app, then render `Zauber:NotFoundComponent`), ZA-165 (a redirect's domain is ignored).
- **Baseline:** ZA-131 (`Directory.Build.props`, analyzers, central package management, `.editorconfig`,
  `.gitattributes`, an SDK pin, a vulnerability scan in CI); CI and release hardening ZA-089, ZA-109.
- **Packaging and template:** ZA-087 (npm manifests in the Components package), ZA-094 (pinned asset package in the
  template), ZA-118 (un-skips its waiting test), ZA-119 (package metadata), ZA-123 (template rough edges).
- **Start-up and DI:** ZA-120 (discovery and configuration fail silently), ZA-167 (a list setting adds to its
  defaults instead of replacing them: `ImageResize:ContentRoots`), ZA-121 (DI leftovers, among them the
  unused `IdentityUserAccessor` that `ICurrentUserAccessor` replaces).
- **The deliberate breaking batch for v5:** internalise non-contract types (ZA-092), move Radzen-typed contracts
  out of Core (ZA-091), remove the unwired MVC path (ZA-090), naming conventions (ZA-124), split the very large
  services and remove `Content.Url` (ZA-125), give interactive custom components an id or DTO instead of the whole
  `Content` (ZA-126).
- The method-scoped DbContexts → `IDbContextFactory` (a known deviation in [Standards.md](Standards.md); no finding
  needs it since ZA-011 was fixed in Phase 10). Do it here or drop it from the deviations list.

#### Phase 11a — The not-found page and redirect domains — DONE (2026-11-27)
ZA-164 (Medium); ZA-165, ZA-166 (Low) fixed. Decisions D41 (not-found answer) and D42 (redirect domains), taken
Umbraco's way as delegated.
- **The not-found page (ZA-164, D41).** Reproduced on the dev site: `/nope-page` answered 404 with
  `Content-Length: 0`. The same in a bare .NET 10 Blazor app (`dotnet new blazor`, no `NotFoundPage`, no status-code
  pages, a catch-all page that sets `Response.StatusCode = 404`): 404 and no body. It is the framework:
  `RazorComponentEndpointInvoker` (.NET 10) skips writing the rendered HTML when the status is 404, the request is not
  a re-execution and no `NotFoundPage` path was set, so that `UseStatusCodePages` can re-execute. In the bare app a
  404 set from `Response.OnStarting` kept the HTML. New `HttpContext.SetStatusCodeWhenResponseStarts(int)` (Core,
  `ZauberCMS.Core.Extensions`) does that and changes only a response still at 200 when it starts, so a redirect
  (302) or the error page (500) that comes later keeps its status. `EntryPage` sets 404 itself whenever it has no
  page for the address (any `NotFoundComponent`, also one that is not found), so a site's view needs no code;
  `NotFound404` and the registration page when it is off use the helper too. A null `NotFoundComponent` no longer
  throws.
- **Redirect domains (ZA-165, D42).** `RedirectMiddleware` keeps each redirect's `DomainId` in its compiled list
  (redirects for a domain before the ones for all, each newest first) and, only when some redirect has a domain,
  reads the request's domains from the cached domain list (`GetCachedDomainsAsync`, cleared when a domain is saved)
  on each request, so an edited domain is matched by its new address. `InvokeAsync`'s signature is unchanged
  (`IContentService` comes from the request's services when needed). `RedirectRules.FindLoop` skips rules for another
  domain and orders like the middleware.
- **Domain matching (ZA-166, new and fixed).** The rule is now one internal helper, `DomainMatching` (Core,
  `ZauberCMS.Core.Content`), used by content routing (`MatchDomainWithContent`) and redirects. Found while extracting
  it: a domain's path matched as a prefix of characters (`example.com/en` matched `/english`), and of two domains that
  both matched (`example.com` and `example.com/fr`) the first in the list won. Now the path matches whole segments
  (and a trailing `/` on the domain is ignored), and the longest matching domain is the request's.
- Tests: `Bootstrap/NotFoundStatusTests` (over HTTP: a Blazor app on Kestrel at a free port; the helper keeps the
  HTML at 404; a 404 set during the render is sent empty, which tells us if .NET changes it; a redirect and the error
  page after the helper keep 302 and 500; a found page stays 200), `Seo/RedirectDomainTests` (ZA165 domain only,
  every host, domain wins for the same address, regex, a domain with a path, an edited domain, loops across
  domains; ZA166 segment matching and the longest domain). Full suite 1016 (1015 pass, 1 skipped for ZA-118) on
  SQLite, PostgreSQL, SQL Server LocalDB and Redis; Release build 0 warnings.
- Verified on the dev site (`ZauberCMS.Web`), Development and Production: `/nope-page` and `/nope/deeper` answer 404
  with the not-found view's HTML (title "404 Missing Page"); `/` 200; `/media/nope.jpg` 404 from the static files
  (no view, as before); `/admin` 302 to the login page; `/Account/Register` 200 (registration on). Not checked in a
  browser: the registration page with registration off (the same helper; covered by the HTTP tests' mechanism) and a
  redirect with a domain on a running site (no account in the tracked `app.db`; the middleware tests cover it).
- **Release notes (D18):**
  - Addresses with no page now get the site's not-found view with status 404; since the move to .NET 10 they were
    sent as an empty 404. ZauberCMS sets the status: a site's own `NotFoundComponent` that sets
    `Response.StatusCode = 404` itself should drop that line (the line makes .NET send the page empty). A site page
    that shows a not-found view of its own calls `HttpContext.SetStatusCodeWhenResponseStarts(404)`.
  - Redirects with a domain now apply only on that domain (they applied on every host); for the same From URL the
    one for the request's domain wins over one for all domains. Redirects without a domain are unchanged.
  - A domain with a path (`example.com/en`) now matches only `/en` and the addresses below it (not `/english`), and
    of two domains that both match a request the longer one wins (it was the first in the list).
  - New public `HttpContextExtensions.SetStatusCodeWhenResponseStarts`. Breaking: none.

#### Phase 11b — Engineering baseline, CI and release hardening — DONE (2026-11-27)
ZA-089 (Medium); ZA-109, ZA-131 (Low) fixed. Decisions D43 (analysers and warnings) and D44 (one version from one
commit), taken as delegated.
- **Build baseline (ZA-131).** `Directory.Build.props` (repository root): target framework, nullable, implicit
  usings, the analysers (D43), warnings as errors, NuGet audit of direct and transitive packages, and the four
  packages' shared version and metadata (`Version`, authors, licence, URLs, tags), plus the `CopyNuGetPackage` target
  the four csprojs repeated. `LangVersion` is left to the SDK (it follows the TFM). `tests/Directory.Build.props`
  imports it and keeps the test settings. `Directory.Packages.props`: central package management, every version
  once. The four packages no longer carry a `PackageReference` to each other next to the `ProjectReference`; the
  packed dependency comes from the `ProjectReference` and its `Version`, so CI and `release.ps1` stamp one line.
  `.editorconfig` (indentation as the files already are, the Standards' code style as IDE suggestions, the
  analyser levels with reasons), `.gitattributes` (`* text=auto` and binary types: every tracked text file is
  already LF in the repository, so `git add --renormalize` changes nothing), `global.json` pins the SDK to the 10.0
  band (`10.0.400`, `rollForward: latestFeature`, no previews) and keeps the test runner. CI's build job takes its
  SDK from `global.json` and ends with a scan that fails on any vulnerable package, direct or transitive
  (`dotnet package list --vulnerable --include-transitive`; it exits 0 either way, so the step reads its JSON).
- **The template stays self-contained.** `ZauberCMS.Template/Directory.Build.props` (empty) and
  `Directory.Packages.props` (central management off) stop the repository's files for the template package and the
  template site; they sit beside `template/`, so they are not packed. The template site keeps its explicit versions.
- **Analyser hits fixed (D43).** About 100: culture-dependent formatting and parsing (cache keys, HTML attributes,
  user names, the installer's row count, `SplitStringUsing`, the validator's property paths) now invariant;
  `EnsureNotStartsWith`/`EnsureNotEndsWith` compare the character ordinally; view paths compared ordinally;
  `Any()` on lists and arrays replaced by `Count`/`Length`; private members that use no instance data made static;
  a shared `JsonSerializerOptions` for the content type export; `Regex.Count`; `MD5.HashData`; the generic
  `CreateDelegate`; `GC.SuppressFinalize` in the modal's `Dispose`; a fixed log template for Identity errors;
  `InvalidOperationException` instead of `NullReferenceException`/`Exception` (the validator without an `EditForm`,
  an unknown database provider); Components declares English as its neutral resource language. Suppressed at the
  site with the reason: the four EF search queries (`ToLower()` is translated to SQL `LOWER()`; the culture overloads
  are not, ZA-017), the MD5 behind the obsolete `ToMd5` (ZA-031), the public mutable statics of `AssemblyManager`
  (ZA-092, 11f).
- **CI and release (ZA-089, ZA-109, D44).** `.github/workflows/ci.yml`: `permissions: contents: read` for the
  workflow and `contents: write` only for the publish job; `actions/checkout` and `actions/setup-dotnet` pinned by
  commit (tag in a comment) and moved to their latest majors; the build job's checkout keeps no credentials;
  `paths-ignore` for Markdown, `Docs/`, `.audit/`, `.claude/`, IDE folders and `LICENSE`. The publish job reserves
  the version first (pushes the tag; fails if it exists on another commit or was pushed meanwhile), checks that the
  five packages were built, pushes them in dependency order (`--skip-duplicate` only on a rerun of the tagged
  commit), then commits the stamped version back. Version parsing accepts `X.Y.Z-label`; a version that is not
  semver fails the run. `release.ps1` reads and stamps `Directory.Build.props` and the template's two csprojs,
  keeps each file's encoding and no longer adds a blank line per run (the 40 it had added are removed).
- Tests: `Packaging/BuildBaselineTests` (no `Version` on a solution `PackageReference` and each one in
  `Directory.Packages.props`; one `Version` for the four packages; the template's stop files; the template site's
  explicit versions; the SDK pin; actions pinned by commit; read-only default permissions; tag pushed before the
  build and the package pushes, in dependency order), `Shared/CultureInvariantTests` (`SplitStringUsing` under
  de-DE read "1.5" as 15; a soft hyphen made `EnsureNotStartsWith` drop the first letter; both seen to fail before
  the fix). Full suite 1026 (1025 pass, 1 skipped for ZA-118) on SQLite, PostgreSQL, SQL Server LocalDB and Redis;
  Release build 0 warnings, also with `--no-incremental`. Dev site (`ZauberCMS.Web`, Release) smoke-run: `/` 200,
  `/nope-page` 404 with the not-found view, `/admin` 302 to the login page, `/Account/Login` 200, nothing logged as
  an error.
- Packages: the four packages and the template were packed before and after; their nuspecs (dependencies and
  versions, `exclude` flags, metadata) and file lists are the same, and every file except the assemblies has the
  same size. `dotnet msbuild -getProperty` on the template site shows the template's stop files in use
  (`ManagePackageVersionsCentrally` false, no warnings as errors, analysers at the SDK default).
- **Not run: CI itself.** It runs only on GitHub, and nothing was pushed to `main-static`. Checked locally instead:
  `actionlint` 1.7.12 with `shellcheck` 0.11.0 (clean); the version, stamp and commit-back steps run as written
  against a scratch git repository with a bare origin (first release, rerun, partial failure, a set major, a stale
  lower version, a prerelease and its second push, a prerelease below a release, a non-semver version, a tag only on
  the remote, a tag pushed between fetch and push, two tags on HEAD); the scan step against the solution (passes)
  and against a project with a vulnerable package (fails); `release.ps1`'s functions against copies of the three
  files (round trip byte for byte). The first push to `main-static` after this is the first real run.
- **Release notes (D18):**
  - Build and contribution only; nothing a site sees changes, except: `StringExtensions.SplitStringUsing` parses
    numbers with the invariant culture (it used the server's: "1.5" was 15 under de-DE), and
    `EnsureNotStartsWith`/`EnsureNotEndsWith` compare the character exactly.
  - `FluentValidationValidator` without a cascading `EditContext` throws `InvalidOperationException` (was
    `NullReferenceException`); an unknown `Zauber:DatabaseProvider` throws `InvalidOperationException` (was
    `Exception`, which still catches it).
  - Identity errors are logged with a fixed template (`Identity error {Code}: {Description}`).
  - Breaking: none.

#### Phase 11c — Packaging and the template — DONE (2026-11-27)
ZA-087, ZA-094 (Medium); ZA-118, ZA-119, ZA-123 (Low) fixed; ZA-168 (Low) found and fixed; ZA-167 (Low) found and
assigned to 11d. The template smoke test Phase 2 left for here is built and runs in CI. Decisions D45 (package
metadata and dependencies), D46 (the smoke test), D47 (the template's `.gitignore` and profiles), taken as
delegated.
- **npm manifests (ZA-087).** The Components package shipped `package.json` and `package-lock.json` as content and
  contentFiles, so they appeared in every site's project (and its publish output); the exclusion pointed at a
  path on one machine (`..\Users\…\zaubercms.rte\1.0.0-beta9`). They are now `None` items: in the IDE, not packed.
- **Framework scripts (ZA-094).** The template site pinned `Microsoft.AspNetCore.App.Internal.Assets` 10.0.0, which
  nothing ever moved, so new sites served the 10.0.0 `blazor.web.js`. It sets `RequiresAspNetWebAssets` instead:
  the SDK adds the asset package of its own ASP.NET Core version (10.0.15 with SDK 10.0.404). The Web SDK sets it
  by itself only when the site has `.razor` files of its own, which a site without the starter views has not.
- **Packaged settings (ZA-118).** `ZauberCMS/appsettings.json` (copied into a site that has none) gets the
  `Zauber:ImageResize` section the template and Web files have; the parity test waiting for it is un-skipped.
- **Package metadata (ZA-119, D45).** `Directory.Build.props`: `PackageReadmeFile` (`ReadMe.md`), `PackageIcon`
  (the template's existing logo, `ZauberCMS.Template/images/logo.png`), `RepositoryType`, Source Link
  (`PublishRepositoryUrl`, `EmbedUntrackedSources`), symbol packages (`IncludeSymbols`, `snupkg`), `Deterministic`,
  and `ContinuousIntegrationBuild` on GitHub Actions. `CopyNuGetPackage` copies each `.snupkg` to `NugetSource/`
  with its package; CI checks the four symbol packages and `dotnet nuget push` sends them with the packages. The
  project-wide `NoWarn CS0618` in Components is gone: its one obsolete use (the content editor's URL field bound
  to `Content.Url`) goes through a private property with a local suppression and the reason (ZA-125 retires the
  property in 11g). Components, Routing and ZauberCMS no longer repeat Core's packages (EF Core providers, Identity,
  Radzen, ImageResize, ZauberCMS.RTE, the EF tools and more); Serilog.Sinks.Async stays on ZauberCMS. Cruft removed:
  `UpToDateCheckInput`, `Compile Remove`, `Folder` and `AdditionalFiles` entries for files that do not exist or
  that the Razor SDK adds by itself, and ZauberCMS's `wwwroot` content item (it has no `wwwroot`). Kept: Core's
  `lib/…/ZauberCMS.Core.runtimeconfig.json`, which EF's design package asks for because `migrations.ps1` runs
  `dotnet ef` with Core as the start-up project.
- **Template rough edges (ZA-123, D47).** The launch profile "Umbraco.Web.UI" is now `https`, with an `http` one
  beside it; `template/.gitignore` is new (the template csproj's `NoDefaultExcludes` packs it, and the smoke test
  checks it reaches the site). The sample's `/Home/Error` was already gone (Phase 10e, D33). The starter `_Imports`,
  layout and views came in Phase 7.
- **Logging settings (ZA-168, new and fixed).** Found by the smoke test's log check: the three `appsettings.json`
  files logged to `Logs\log.txt`, which on Linux is a file named `Logs\log<date>.txt` in the content root, not a
  `Logs` folder; and they listed the `WithThreadId` and `WithMachineName` enrichers, whose packages ZauberCMS does
  not reference, so Serilog skipped them. Now `Logs/log.txt` and `FromLogContext` only, the same in the three files.
- **Found (ZA-167, to 11d).** `ImageResizeSettings.ContentRoots` starts as `["img", "images", "media"]` and the
  configuration binder adds the configured list to it: with the shipped settings a site has each root twice, and a
  site that sets its own roots cannot drop the defaults. Seen with the binder in a scratch program.
- **Template smoke test (D46).** `tests/template-smoke.ps1`: builds the four packages and packs the template at
  `<Version>-smoke.<UTC time>` into a local feed, installs the template in a private hive (`--debug:custom-hive`),
  creates `SmokeSite`, checks its files, restores exactly that version, builds and publishes it, starts it in
  Production on a free port, goes through the installer over HTTP (SQLite, the starter kit) and checks the home
  page, `blazor.web.js` (fingerprinted and plain), a 404 with its page, the admin signed in and each of its 11
  scripts and styles (Radzen, BlazorMonaco, the Components assets: the transitive static web assets after the
  dependency change), and the site's log. It then removes the version from the NuGet cache and `bin`, and the work
  folder. CI: a `pwsh` step at the end of the build job.
- Tests: `Packaging/PackageContentsTests` (ZA087 the manifests are not content and no machine path is left; ZA119
  readme, icon, Source Link and symbols set and their files exist, no project hides CS0618, no package repeats what
  its project references bring; ZA094 `RequiresAspNetWebAssets` and no pinned asset package; ZA123 `.gitignore`,
  `NoDefaultExcludes`, no "Umbraco" profile, no `/Home/Error`; CI runs the smoke test in the build job),
  `AppSettingsParityTests` (ZA-118's test un-skipped; ZA168 the Serilog section is the same in the three files,
  forward slashes, only `FromLogContext`). All nine were run against the previous commit and failed there. Full
  suite 1034 (all pass, none skipped) on SQLite, PostgreSQL, SQL Server LocalDB and Redis; Release build 0
  warnings, also with `--no-incremental`.
- Smoke test runs: Windows PowerShell 5.1 on Windows (passes; the first run found the 303 after the installer's
  posts and the expected "no database" error, and the script was corrected), and PowerShell 7.6 in the
  `mcr.microsoft.com/dotnet/sdk:10.0` Linux container with `GITHUB_ACTIONS=true` on a copy of the working tree
  (passes, all 44 checks, 54 seconds). Afterwards the NuGet cache, the repository's `bin` folders and the machine's installed
  templates (ZauberCMS.Template 4.1.0 from a local feed) were as before. Dev site (`ZauberCMS.Web`, Release,
  Production): `/` 200, `/nope-page` 404 with the not-found view, `/admin` 302 to the login page,
  `/Account/Login` 200, `_framework/blazor.web.js` 200, logs in `Logs/`.
- Packages before and after (all five packed from the previous commit and from this one, file lists and nuspecs
  compared): Core, Components, Routing, ZauberCMS gain `ReadMe.md`, `logo.png`, `<readme>`, `<icon>` and the
  branch in `<repository>`, and a `.snupkg` with the PDB (Source Link to the commit checked in it). Components
  loses `content/` and `contentFiles/` `package.json` and `package-lock.json`, its `contentFiles` nuspec section,
  its `runtimeconfig.json`, and 9 dependencies (ImageResize, Components.Web, Diagnostics.EF, Identity.EF, EF
  SqlServer, EF Sqlite, Localization, Radzen, ZauberCMS.RTE); Routing loses 3 (Components.Web, EF Sqlite,
  Localization); ZauberCMS loses its `runtimeconfig.json` and 11 (Humanizer, MailKit, Components.Web,
  Diagnostics.EF, Identity.EF, EF SqlServer, EF Sqlite, Localization, Logging.Abstractions, Radzen,
  Serilog.AspNetCore), all still reached through Core (or Components, for Humanizer) at the same versions. The
  template gains `content/template/.gitignore`; its csproj and launch settings changed inside.
- **Not verified:** CI itself (GitHub only; nothing pushed to `main-static`); a push of a `.snupkg` to nuget.org
  (the first release is the first); how nuget.org renders `ReadMe.md` (its images are on gitbook.io, which
  nuget.org may not show).
- **Release notes (D18):**
  - The packages now have a readme and the ZauberCMS icon on nuget.org, and symbol packages with Source Link:
    a debugger can step into ZauberCMS from a site.
  - The Components package no longer puts `package.json` and `package-lock.json` into a site's project and its
    published output. A site that has them from an earlier version can delete them.
  - Components, Routing and ZauberCMS declare fewer direct dependencies; the same packages reach a site through
    ZauberCMS.Core at the same versions.
  - New sites from the template take the framework scripts from the SDK (`RequiresAspNetWebAssets`). **A site made
    from an earlier template** still pins `Microsoft.AspNetCore.App.Internal.Assets` 10.0.0 and serves the old
    `blazor.web.js`: remove that `PackageReference` and add `<RequiresAspNetWebAssets>true</RequiresAspNetWebAssets>`.
  - New sites get a `.gitignore` (the SQLite file, media, the resize cache and logs are left out) and the launch
    profiles `https` and `http`.
  - The shipped settings log to `Logs/log.txt` (on Linux the old `Logs\log.txt` made a file with a backslash in its
    name) and drop two enrichers that had no package. An existing site keeps its own `appsettings.json`; one on
    Linux can change the path itself. The packaged settings now have the `Zauber:ImageResize` section (same values
    as the code's defaults).
  - Breaking: none.

#### Phase 11d — Start-up and DI — DONE (2026-11-27)
ZA-120, ZA-121, ZA-167 (Low) fixed. Decisions D48 (start-up checks), D49 (a configured list replaces the default) and
D50 (DI leftovers), taken as delegated. Also fixed first: the CI run of 11c (2cd1fb6) failed on GitHub's Linux runner
in `PackageContentsTests.ZA119…`, which joined the icon's MSBuild path (`ZauberCMS.Template\images\logo.png`) with
its `\`; on Linux that is part of a file name. The test turns it into the platform's separator (the packing itself
was not affected). That run stopped at the tests, so CI had not yet run the template smoke test; this push is its
first run.
- **Start-up checks (ZA-120, D48).** `ZauberSettingsValidator` (internal, `IValidateOptions<ZauberSettings>`,
  `ValidateOnStart`): a `Zauber:Plugins:StorageProvider`/`EmailProvider`/`CaptchaProvider` that names no discovered
  provider, and an `AdminDefaultLanguage` that is not a culture, stop the start; the message names the setting, the
  value and the providers found. `AddZauberCms(app)` reads the settings first, so it fails before anything is
  mapped. `ProviderService` gives the same message if a wrong name gets past start-up; a blank storage or email
  provider is no provider (it threw `KeyNotFoundException` for an empty string). `FindWarnings`: a `SiteUrl` that is
  not an http(s) address, and a `NotFoundComponent`, `StarterComponent`, `MissingView` or `Identity:AccountLayout`
  that is not found, are warnings at every start. An unknown `Zauber:DatabaseProvider` names the setting and the
  three providers.
- **What the bootstrap skips is logged (ZA-120).** `AddZauberCms(builder)` runs before logging exists, so it
  collects warnings in `StartupMessages` (internal singleton) and `AddZauberCms(app)` writes them through
  `ILoggerFactory` (category `ZauberCMS.Startup`): an assembly whose types cannot all be loaded (the loader errors,
  once per assembly), an `IStartupPlugin`/`IExternalAuthenticationProvider` without a parameterless constructor, and
  a non-public class in the site's or a plugin's assembly that implements a discovery interface (the list is
  `ExtensionManager.DiscoveryInterfaces`; a test checks every name is a public interface). A bootstrap
  constructor that throws now fails with `InvalidOperationException` naming the type (was
  `TargetInvocationException`). The public `GetBootstrapInstances<T>()` is unchanged.
- **Discovery cache (ZA-120).** It was keyed by `T` alone, so `GetImplementations<T>` with a predicate, or with
  `onlyExportedTypes: false`, returned the dictionary of an earlier call, and `AssemblyManager.SetAssemblies` did not
  clear it: a second host in the same process (tests, or a site that builds two hosts) with other assemblies saw the
  first one's types. Now keyed by (type, exported only), a search with a predicate is not cached, `SetAssemblies`
  clears both caches, and a cached result is handed out as a copy (a caller that changed it changed every later
  caller's). `AssemblyManager.Assemblies` is empty, not null, before `SetAssemblies`.
- **Lists in settings (ZA-167, D49).** `ZauberSettingsBinding` reads the `Zauber` section for the bootstrap's copy
  and, as a `Configure` step right after `Bind`, for `IOptions<ZauberSettings>`: every list the configuration sets
  replaces the property's list. ImageResize gets `ContentRoots` once each (`img`, `images`, `media` were there twice
  with the shipped settings) and a site's own list (`["uploads"]`) alone.
- **DI leftovers (ZA-121, D50).** Callers grepped in Core, Components, Routing, ZauberCMS, Web, the template and the
  tests: nothing used them. Removed the scoped `HttpClient`, the `IUserEmailStore<User>` registration and the
  second `SignInManager<User>` registration (`AddSignInManager<ZauberSignInManager>()` registers it once and
  `ZauberSignInManager` as the same instance). `[Obsolete]`: `IdentityUserAccessor` (still registered),
  `UserEmailStore`, `ContentNavGroup.Actions`.
- Tests: `Bootstrap/StartupDiagnosticsTests` (16): ZA120 a mistyped storage and email provider (the options and
  `IStartupValidator` both fail, the message lists the real provider), a mistyped culture, the defaults, a site
  without a database and a blank provider pass, the three shipped `appsettings.json` files pass with no warning, the
  warnings, the runtime message, the bootstrap warnings collected before logging and logged after (a test-assembly
  start-up plugin without a parameterless constructor and an internal `ISeedData`), the interface list, the cache;
  ZA121 the registrations; ZA167 a configured list replaces the default in both options, the shipped list once, a
  guard over every list setting with a default. `FormProtectionTests` changed: a wrong CAPTCHA name now fails at
  start-up (and still throws from `ProviderService`). With the fixes reverted, the cache, `SignInManager` and three
  ZA167 tests failed. Full suite 1050 (all pass, none skipped) on SQLite, PostgreSQL, SQL Server LocalDB and Redis;
  Release build 0 warnings.
- Dev site (`ZauberCMS.Web`, Release, Production): with the shipped settings `/` 200, `/nope-page` 404,
  `/admin` 302 to the login page, `/Account/Login` 200, a `/media` image with `?width=40` resized (2694 of 8622
  bytes), nothing logged as a warning or error; with `Zauber__Plugins__StorageProvider` mistyped the process stops at
  start-up with the `OptionsValidationException` message; with `Zauber__SiteUrl=www.example.com` the warning is logged.
- **Not verified:** the warning for an assembly whose types cannot load (no such assembly in the tests; the code
  path is the one the bootstrap already took, now with a message); a bootstrap constructor that throws.
- **Release notes (D18):**
  - A `Zauber:Plugins:*` provider name that is not found now stops the site at start-up with a message that names
    the setting and lists the providers found (it failed when media or email was first used). An
    `AdminDefaultLanguage` that is not a culture fails with the setting's name. An empty `StorageProvider` or
    `EmailProvider` means none and passes start-up; media upload or email then still fails when used, as before
    (corrected by the Phase 11 audit; ZA-196).
  - New start-up warnings (category `ZauberCMS.Startup`): a non-public extension type, a start-up plugin or external
    login provider without a parameterless constructor, types that cannot be loaded, a `SiteUrl` that is not an
    http(s) address, a fallback view or account layout that is not found.
  - `Zauber:ImageResize:ContentRoots` in a site's settings now replaces the default roots instead of being added to
    them. A site that lists the three defaults (every shipped `appsettings.json`) is unchanged; one that listed
    others now resizes only those, as written. .NET still merges a list across configuration sources by index: a
    shorter list in `appsettings.{Environment}.json` or environment variables keeps the higher indexes of
    `appsettings.json`, so set the whole list in one place (Standards.md §6; added by the Phase 11 audit).
  - A bootstrap extension whose constructor throws fails the start with `InvalidOperationException` naming the type.
  - **Breaking:** `HttpClient` from DI is the framework's default client (no base address); it was ZauberCMS's own
    with the request's address as base. A site that injects `HttpClient` and uses relative URLs gives them a base
    itself, or registers a named client (`AddHttpClient("site", c => c.BaseAddress = …)`).
  - **Breaking:** `IUserEmailStore<User>` is no longer registered. Use `UserManager<User>` (`FindByEmailAsync`,
    `SetEmailAsync`) or cast `IUserStore<User>`, which the Entity Framework store implements.
  - Obsolete, to be removed in a later major: `IdentityUserAccessor` (use `ICurrentUserAccessor.GetUserAsync`),
    `UserEmailStore`, `ContentNavGroup.Actions`.

#### Phase 11e — The MVC path removed, UI contracts out of Core — DONE (2026-11-27)
ZA-090, ZA-091 (Medium) fixed. Decisions D51 (UI contracts in Components) and D52 (the MVC path removed), taken
Umbraco's way as delegated. The first part of the v5 breaking batch (D18).
- **The MVC path (ZA-090, D52).** Callers grepped in Core, Components, Routing, ZauberCMS, Web, the template, the
  tests and the docs: nothing live used `IContentFinder`, `ContentFinderPipeline`, `ZauberRouteValueTransformer`,
  `ZauberCMS.Core.Rendering`, `HtmlHelperExtensions`, `RenderExtensions` (only a commented-out call in
  `ContentTypeEditor`), Routing's views and view components, or `IContentBlockView.RenderMode`. `IContentFinder`
  was not in the discovery list (`ExtensionManager.DiscoveryInterfaces`), AGENTS.md's extension table or the docs,
  only in the "currently unwired" note. All removed, with the commented-out routes, their registrations and
  Routing's `AddRazorSupportForMvc`. To find what the path provided implicitly, the bootstrap's service collection
  was listed before and after: 53 registrations went, 51 of them MVC view, Razor view engine, tag helper and TempData
  services plus the two removed ones. Of what live code uses, `IMemoryCache` (ServerCache, the confirmation
  re-sends, the form tokens) and a memory `IDistributedCache` are now registered by `AddZauberCms` itself; the
  memory one does not replace Redis's (`AddStackExchangeRedisCache` registers first). One live user of the view
  services was found by a test: `AuthController`'s sign-out POST had `[ValidateAntiForgeryToken]`, whose filter
  is a view service; with `AddControllers` alone it answered 500. It now calls `IAntiforgery.IsRequestValidAsync`
  and answers 400 without a valid token, as the attribute did.
- **UI contracts out of Core (ZA-091, D51).** Every Radzen and Blazored use in Core was listed by grep and the
  build: `IContentProperty` and `ISectionNavGroupAction` (contracts), `DialogExtensions`,
  `NotificationServiceExtensions`, `TreeExtensions`, `PropertyExtensions.GetPropertySettings(this
  IContentProperty)`, `ValidateService<T>`, `MediaValidationService`, the vendored modal (`ZauberCMS.Core/Modal`),
  and in `ZauberSetup` `AddRadzenComponents`, `AddBlazoredModal` and the two services' registrations (other hits
  were comments). All moved to ZauberCMS.Components with their full names (`Core/` and `Modal/` folders);
  `GetPropertySettings(this IContentProperty)` is `ContentPropertyExtensions` (same namespace). The registrations
  are in `ZauberCMS.Components.Startup.AdminUiStartup` (internal `IStartupPlugin`). Radzen.Blazor moved from Core's
  package references to Components'. Core keeps Microsoft.AspNetCore.Components.Web (the installer's pages) and
  ZauberCMS.RTE (rich text rendering; it does not depend on Radzen). The modal's script is now
  `_content/ZauberCMS.Components/Modal/BlazoredModal.razor.js` and its scoped CSS is in the Components bundle;
  `App.razor` no longer links `ZauberCMS.Core.bundle.scp.css`, which Core no longer has.
- Tests: `Bootstrap/CoreSurfaceTests` (21): ZA090 no removed type, no class deriving from `RazorPage`,
  `ViewComponent` or `DynamicRouteValueTransformer` and no compiled MVC view in Core, Components or Routing, and
  `IContentBlockView` has no members of its own; the caches are registered, the Razor view engine is not, Redis's
  `IDistributedCache` stays; the sign-out POST over HTTP (Kestrel): 400 without the token, 302 with the token from
  the GET page (with `[ValidateAntiForgeryToken]` back it answered 500). ZA091 Core references neither Radzen.Blazor
  nor Components and has no `Blazored*` type; each moved type (16) is public in Components under its old full name
  and not in Core; the bootstrap registers Radzen's notification, dialog and context-menu services, the modal,
  `ValidateService<T>` and `MediaValidationService`, and discovery finds `TextProperty` (`IContentProperty`) and
  `CreateContentNavMenu` (`ISectionNavGroupAction`). `PersistedTypeNameTests` passes unchanged (no editor name
  changed). Full suite 1071 (all pass, none skipped) on SQLite, PostgreSQL, SQL Server LocalDB and Redis; Release
  build 0 warnings; the template smoke test passes.
- Dev site (`ZauberCMS.Web`, Release, Production, on a copy of its `app.db`): `/`, `/about` (its blocks render),
  `/contact-us`, `/blog` 200, `/nope-page` 404, `/Account/Login` 200, `/admin` 302 to the login page signed out, a
  `/media` image with `?width=40` resized (3527 of 132637 bytes), `/api/auth/logout` 200. Signed in (Playwright):
  the admin loads; editing a block on About opens the side panel, its script is served from
  `_content/ZauberCMS.Components/Modal/` and its scoped CSS applies; the Content group's "…" menu shows its actions
  and "Sort Content" shows a Radzen notification; opening `/api/auth/logout` signs out (then `/admin` goes to the
  login page). Nothing logged as an error; the browser console shows only a missing favicon.
- **Not verified:** a third-party plugin built against the 4.x packages (a binary break by design).
- **Release notes (D18):**
  - **Breaking:** the unwired MVC rendering path is removed (it never ran: its routes were not mapped):
    `ZauberCMS.Core.Content.Interfaces.IContentFinder`, `ZauberCMS.Core.Content.ContentFinders.ContentFinderPipeline`
    and `DefaultContentFinder`, `ZauberCMS.Core.Shared.ZauberRouteValueTransformer`, `ZauberCMS.Core.Rendering.*`
    (`ZauberViewPage<T>`, `IZauberViewPage`, `ZauberPageViewModel`), `ZauberCMS.Core.Extensions.HtmlHelperExtensions`
    (`GenerateMetaTags`) and `RenderExtensions` (`GetAllowedViews`, `GetViewIdentifier`, `GetViewDetails`), and the
    ZauberCMS.Routing views (`Views/ZauberRender/*`, `_ZauberLayout`, the `RenderBlocks` and `BootstrapPager` view
    components with `RenderBlocksModel`). A class that implemented `IContentFinder` no longer compiles; it was never
    called.
  - **Breaking:** `IContentBlockView.RenderMode` is removed (nothing read it). A block's own `RenderMode` property
    still compiles; one that implements it explicitly (`IContentBlockView.RenderMode`) drops that.
  - **Breaking:** ZauberCMS registers MVC controllers and MVC's view features (`AddMvcCore().AddViews()`: the
    antiforgery filters, TempData, view components, HTML helpers), but no Razor view engine. A site with its own
    Razor views (`.cshtml`), Razor Pages or tag helpers calls `builder.Services.AddControllersWithViews()`.
    `IMemoryCache` and `IDistributedCache` (memory, unless Redis is configured) stay registered. Controllers with
    `[ValidateAntiForgeryToken]`, `[AutoValidateAntiforgeryToken]` or `[IgnoreAntiforgeryToken]` work as in 4.x (the
    view features were dropped in 11e and registered again by the Phase 11 audit follow-ups, ZA-185, D60).
  - **Breaking (binary):** these types moved from the ZauberCMS.Core assembly to ZauberCMS.Components, with the same
    namespaces and names: `ZauberCMS.Core.Content.Interfaces.IContentProperty`,
    `ZauberCMS.Core.Sections.Interfaces.ISectionNavGroupAction`, `ZauberCMS.Core.Extensions.DialogExtensions`,
    `NotificationServiceExtensions`, `TreeExtensions`, `ZauberCMS.Core.Media.Services.MediaValidationService`,
    `ZauberCMS.Core.Shared.Services.ValidateService<T>`, and the whole vendored modal: `Blazored.Modal`
    (`BlazoredModal`, `BlazoredModalInstance`, `CascadingBlazoredModal`, `FocusTrap`, `IModalReference`,
    `ModalReference`, `ModalOptions`, `ModalParameters`, `ModalPosition`, `ModalSize`, `ModalAnimationType`,
    `ServiceCollectionExtensions.AddBlazoredModal`) and `Blazored.Modal.Services` (`IModalService`, `ModalService`,
    `ModalResult`). Source that uses them compiles unchanged when the project references ZauberCMS.Components or the
    `ZauberCMS` package; a library that referenced only ZauberCMS.Core adds that reference, and a plugin built
    against 4.x is rebuilt.
  - **Breaking:** `PropertyExtensions.GetPropertySettings<T>(this IContentProperty)` is now
    `ContentPropertyExtensions.GetPropertySettings<T>` in ZauberCMS.Components (same namespace,
    `ZauberCMS.Core.Extensions`); `cp.GetPropertySettings<T>()` compiles unchanged, a static call by class name
    changes the class name. The `IContentPropertySettings<T>` overloads stay in Core.
  - ZauberCMS.Core no longer depends on Radzen.Blazor (ZauberCMS.Components does) and has no scoped CSS bundle:
    the modal's script is at `_content/ZauberCMS.Components/Modal/BlazoredModal.razor.js` and its CSS is in
    `ZauberCMS.Components.bundle.scp.css`. A site's own layout that linked
    `_content/ZauberCMS.Core/ZauberCMS.Core.bundle.scp.css` removes that link.
  - The sign-out POST (`/api/auth/logout`) still answers 400 without a valid antiforgery token; it checks the token
    itself now.

#### Phase 11f — Public surface and API conventions — DONE (2026-11-27)
ZA-092 (Medium) and ZA-124 (Low) fixed. Decisions D53 (what is public) and D54 (API conventions), taken Umbraco's
way as delegated. The second part of the v5 breaking batch (D18).
- **What is public (ZA-092, D53).** The public types of the four packages were listed from their metadata before
  and after: **ZauberCMS.Core 429 → 375**, **ZauberCMS.Components 278 → 274**, ZauberCMS.Routing 2 → 2, ZauberCMS
  4 → 4 (the last two hold only Razor components and `_Imports`). The rule (Standards.md §1, "What is public") keeps
  public what a site builds on, as Umbraco does with its service implementations; what is left in Core is mostly
  parameters, models, interfaces, services, extension methods and the built-in extension types, which discovery can
  only see when they are public. Each candidate was grepped in every project, the template, the tests and the docs;
  a type another package uses stays public instead of getting `InternalsVisibleTo` (the packages use each other
  through public API only), so `CultureMiddleware` (EntryPage calls `GetEntryModel`), `AdminAccess` (App and Routes),
  `ServerCache` (in `DefaultCacheService`'s constructor), `InstallState`, `CultureLookup`, `AdminCulture`,
  `RedirectRules`, `SeoHttp` and the `MapTo` copiers (the admin's `ContentEditor` uses one) stay public. Made
  internal (54 in Core, 2 in Components, and two nested classes private): the EF entity mappings, the migrations,
  the design-time factories, six middleware classes and `SitePaths`, `DailyJob`, `AdminCultureCircuitHandler`, the
  endpoint mapping of the account pages, `AssemblyManager` (its unused public mutable `Types`/`TypesByName` removed),
  and in Components the sitemap start-up plugin and its hosted service. Three unused public types are `[Obsolete]`
  (D50): `ZauberCMS.Core.Shared.Dictionary<TKey1,TKey2,TValue>`, `IMsSqlMigration`, `ISqLiteMigration`.
- **Migrations.** EF generates a migration `public`; `migrations.ps1` now rewrites a new migration's class to
  `internal` after `dotnet ef migrations add`. Checked by generating an empty migration on all three providers with
  the script (each came out `internal`, Up/Down empty, snapshots unchanged; removed afterwards) and by
  `dotnet ef migrations list`, which lists the internal ones. The design-time factories are internal and
  `dotnet ef` still finds them (the script ran through them). Docs/Data.md says so.
- **API conventions (ZA-124, D54).** `QueryContentParameters` has `IncludeDeleted` and `OnlyDeleted` like the other
  parameter classes; `IsDeleted` is an `[Obsolete]` alias (`false` = neither, `true` = `OnlyDeleted`, `null` =
  `IncludeDeleted`), and the default still returns only live items. Callers grepped: only tests used `IsDeleted`
  (moved to the new names); the admin's recycle-bin paths use `GetContentParameters.IncludeDeleted`, unchanged.
  `Cached` defaults unchanged, with the rule written down and each default pinned. `AuditExtensions.AddAudit` takes
  `CancellationToken cancellationToken = default` instead of `CancellationToken?`; `ExportContentTypeAsync` and
  `ImportContentTypeAsync` take a token and pass it to their queries and the type save; the import's content save after
  the type is saved ignores it, so a caller that has gone does not stop the content once the type is saved (the
  known deviation in Standards.md is gone; the two are still separate saves, so a failing content save leaves the type
  without its content, ZA-184); `SaveChangesAndLog` (both) and `IQueryModel.ExecuteQuery` give theirs a default. Discovery
  interfaces are not moved; the convention for new ones is written down (Standards.md §11).
- Tests: `Bootstrap/PublicApiTests` (9): the public types of Core, Components and Routing equal
  `Bootstrap/Snapshots/PublicTypes.txt` (651 types; a new public type has to be added on purpose, a removed one is
  named as a break); no public EF mapping, migration, model snapshot, design-time factory, hosted service, circuit
  handler or middleware (but `CultureMiddleware`); every built-in class implementing a discovery interface is public;
  on SQL Server, SQLite and PostgreSQL EF lists exactly the internal migrations of that context and applies every
  internal mapping (each entity in its `Zauber*` table). `Bootstrap/ApiConventionTests` (17): no parameter class
  returns recycled items by default; the recycle-bin flags are only `IncludeDeleted`/`OnlyDeleted` (an obsolete
  alias aside); each `Cached` default pinned and every parameter class with `Cached` in the list; every public
  method's token is last, optional and not nullable (with the written exceptions); every async method of a service
  interface takes a token (`ICacheService` aside). `ContentSoftDeleteTests`: `IncludeDeleted` returns both,
  `OnlyDeleted` only recycled, the obsolete `IsDeleted` filters as before for `false`, `true` and `null`.
  Full suite 1109 (all pass, none skipped) on SQLite, PostgreSQL, SQL Server LocalDB and Redis; Release build 0
  warnings; the template smoke test passes.
- Dev site (`ZauberCMS.Web`, Release, Production, on a copy of its `app.db`): `/`, `/about`, `/contact-us`, `/blog`
  200, `/nope-page` 404, `/Account/Login` 200, `/admin` 302 to the login page signed out, a `/media` image 200 with
  `X-Content-Type-Options` and resized with `?width=40` (1878 of 610998 bytes), `/api/auth/logout` 200. Signed in
  (Playwright; an account registered on the copy and promoted with `RecoverAdminUser`): the admin loads, the content
  tree and the About editor with its blocks, Settings, SEO Redirects and its "Create Redirect" dialog with the
  domain list ("Any"). Nothing logged as an error; the browser console shows only the missing favicon and the 404
  of the access-denied page before the promoted account signed in again.
- **Not verified:** a third-party plugin built against the 4.x packages (a binary break by design); the media
  picker's settings dialog (its nested `EnumObject` is private now) was not opened in the browser, only built.
- **Release notes (D18):**
  - **Breaking:** these types are internal now (a site never had a reason to use them; ZauberCMS wires them itself):
    - EF entity mappings: `ZauberCMS.Core.Audit.Mapping.AuditDbMapper`; `ZauberCMS.Core.Content.Mapping.`
      `ContentDbMapping`, `ContentPropertyValueDbMapping`, `ContentTypeDbMapping`, `ContentVersionDbMapping`,
      `DomainDbMapping`, `UnpublishedContentDbMapping`; `ZauberCMS.Core.Data.Mapping.GlobalDataDbMapping`;
      `ZauberCMS.Core.Languages.Mapping.LanguageDbMapping`, `LanguageDictionaryDbMapping`, `LanguageTextDbMapping`;
      `ZauberCMS.Core.Media.Mapping.MediaDbMapping`; `ZauberCMS.Core.Membership.Mapping.ContentRoleDbMapping`,
      `MediaRoleDbMapping`, `RoleClaimDbMapping`, `RoleDbMapping`, `UserClaimDbMapping`, `UserDbMapping`,
      `UserLoginDbMapping`, `UserPropertyValueDbMapping`, `UserRoleDbMapping`, `UserTokenDbMapping`;
      `ZauberCMS.Core.Seo.Mapping.SeoRedirectDbMapping`; `ZauberCMS.Core.Tags.Mapping.TagDbMapping`,
      `TagItemDbMapping`. The `*MappingExtensions` (`MapTo`) stay public.
    - EF migrations: every class in `ZauberCMS.Core.Data.Migrations.SqlServer`, `.SqLite` and `.PostgreSql`
      (`Initial`, `AddDateUpdatedConcurrencyToken`, `AddUserDeletedFlag`, `ContentVersionIndexes`,
      `RedirectIsRegex`), and the design-time factories `ZauberCMS.Core.Data.ZauberDesignTimeDbContextFactory`,
      `ZauberSqliteDesignTimeDbContextFactory`, `ZauberPostgreSqlDesignTimeDbContextFactory`. The DbContexts stay
      public.
    - Middleware and request plumbing: `ZauberCMS.Core.Installer.Middleware.InstallerMiddleware`,
      `ZauberCMS.Core.Media.Middleware.MediaSecurityHeadersMiddleware`, `RestrictedMediaMiddleware`,
      `ZauberCMS.Core.Membership.Middleware.AdminAccessMiddleware`, `ZauberCMS.Core.Middleware.ErrorPageMiddleware`,
      `RedirectMiddleware`, `SitePaths`, and `ZauberCMS.Core.Membership.IdentityComponentsEndpointRouteBuilderExtensions`
      (`MapAdditionalIdentityEndpoints`, which `AddZauberCms` calls). `CultureMiddleware` and `AdminAccess` stay
      public.
    - Background and circuit plumbing: `ZauberCMS.Core.Jobs.DailyJob`,
      `ZauberCMS.Core.Languages.AdminCultureCircuitHandler`, `ZauberCMS.Components.Seo.Startup.SeoSitemapStartUp`,
      `ZauberCMS.Components.Seo.Startup.SitemapGeneratorService`.
    - `ZauberCMS.Core.Plugins.AssemblyManager`, with its unused `Types` and `TypesByName` removed. A site adds
      assemblies with `AddZauberCms(params Assembly[])`, and reads the scanned ones with
      `ExtensionManager.GetAssemblies(null)` (what `AssemblyManager.Assemblies` gave). Helpers that went internal with
      their middleware (`SitePaths.IsSystemPath` among them) have no public replacement: a site that used one keeps a
      copy (added by the Phase 11 audit).
    - The nested `MediaPickerPropertySettings.EnumObject` and `AddEditRedirect.DomainChoice` are private.
  - **Breaking:** `IContentService.ExportContentTypeAsync` and `ImportContentTypeAsync` take a
    `CancellationToken cancellationToken = default` (calls compile unchanged; a class that implements
    `IContentService` adds the parameter). `AuditExtensions.AddAudit` takes `CancellationToken cancellationToken =
    default` instead of `CancellationToken?` (a call that passed `null` passes nothing).
  - Obsolete: `QueryContentParameters.IsDeleted` → `IncludeDeleted` (also recycled) or `OnlyDeleted` (only
    recycled); it still works. `ZauberCMS.Core.Shared.Dictionary<TKey1,TKey2,TValue>`, `IMsSqlMigration` and
    `ISqLiteMigration` are unused and obsolete.
  - `SaveChangesAndLog` and `IQueryModel.ExecuteQuery` give their token a default (compiles unchanged).
  - No behaviour change: what every query returns by default, and every `Cached` default, is as in 4.x.

#### Phase 11g — Content finders, the service split, custom components, the DbContext pattern — DONE (2026-11-27)
ZA-125 and ZA-126 (Low) fixed, the owner's request for content finders built (D55), the `IDbContextFactory`
question decided (D59). Decisions D55 (owner, design delegated) and D56–D59 (delegated), Umbraco's way. Two
commits: content finders (6166a62), then the rest. Phase 11 is done with this part.
- **Content finders (owner, 2026-11-27, D55).** `IContentFinder` (`ZauberCMS.Core.Content.Interfaces`, discovered,
  constructor DI, `SortOrder` then type name) with `ContentFinderContext` (`Url`, `Slug`, `Path`, `Segments`,
  `Domain`, `IsRootContent`) and `ContentFinderResult` (page id, `LanguageIsoCode`, `RouteValues`). They run in
  `IContentService.GetContentFromRequestAsync` (`ContentService.Routing.cs`) after the URL lookup finds no live
  page, before the not-found page; the first result wins. The page is loaded again under the live rule with the
  usual includes (shared with the URL lookup, `LoadEntryAsync`), cached under the Content prefix; a finder that
  names a page that is not live, throws, or cannot be built is logged and passed over. `EntryModel` has
  `ContentFinder` and `RouteValues`; `EntryPage` cascades the `EntryModel`. `ExtensionManager.CreateInstance`
  (internal) builds one extension, so one constructor that throws does not stop the others. AGENTS.md lists the
  extension point; Docs/FrontEnd.md has the request-flow step and a worked `/products/{sku}` example.
- **The split (ZA-125, D56).** `ContentService` 2877 lines in one file → eleven partial files, the largest 760
  (`.Save`); `MembershipService` 1248 → three (552, 224, 537). Moved by a script that cuts whole members, checked
  line by line (every line of the old file is in exactly one new file); usings the compiler reported unused in the
  moved files were removed. Nothing else changed in them.
- **`Content.Url` (ZA-125, D57).** No longer `[Obsolete]`; `ContentExtensions.Url()` is, with the exit in its
  message. Every caller moved to the property (Core, Components, the sample site, the tests) and the pragma
  suppressions that were only for it are gone (10 remain, for other obsolete members). No migration.
- **Custom components (ZA-126, D58).** `ICustomContentComponent.Content` → `Guid ContentId`; `RenderCustomContent`
  passes only the id; the sample `ContactForm` takes it. Docs/FrontEnd.md "Interactivity" describes custom
  components and how to load the page; AGENTS.md's row said "custom content tabs", which they are not.
- **DbContext pattern (D59).** Kept; Standards.md §3 states it as the pattern and its "Known deviations" row is
  gone; Services.md gives the reason.
- Tests: `Content/ContentFinderTests` (9): a hit with route values, the context (`Slug`, `Path`, `Url` with its
  query string), a miss, not asked when a page has the URL, order and the first winner, a throwing finder and a
  constructor that throws, unpublished / recycled / unknown pages passed over, a page recycled after it was served
  through a finder not served again (cache cleared), the finder's language, the home page.
  `Content/CustomContentComponentTests` (2): the contract carries no `Content`; `RenderCustomContent`, rendered with
  `HtmlRenderer` on a real site, gives a component that declares only `ContentId` the page's id.
  `Bootstrap/CoreSurfaceTests`: `Content.Url` is not obsolete and `Url()` is; the split services are public, not
  sealed, with one constructor and every interface method; the D52 check now allows the new `IContentFinder`
  (without MVC route values). `PublicApiTests` snapshot: three types added (`IContentFinder`,
  `ContentFinderContext`, `ContentFinderResult`). Full suite 1122 (all pass, none skipped) on SQLite, PostgreSQL,
  SQL Server LocalDB and Redis; Release build 0 warnings; the template smoke test passes.
- Dev site (`ZauberCMS.Web`, Release, Production, on a copy of its `app.db`): with a scratch finder (removed
  afterwards) `/finder-test/ABC-123` and `/finder-test/xyz?x=1` showed the About page with status 200 and the
  finder's name and SKU read in the view from the cascaded `EntryModel`; a finder that threw was logged as an error
  and the address answered 404 with the not-found page; `/finder-test` and `/nope-page` 404. After the rest: `/`,
  `/about`, `/contact-us`, `/blog`, `/Account/Login` 200, `/nope-page` 404, `/admin` 302 signed out, the navigation
  links (`Content.Url`) as before; the contact form (an interactive custom component) filled in and sent in a
  browser (Playwright), no console errors; its prerender descriptor went from 7046 to 777 characters and the page
  from 39953 to 33684 bytes. Nothing logged as an error.
- **Not verified:** the admin was not opened in a browser for this part (its changes are a comment in
  `ContentEditor` and `.Url()` → `.Url` in editors, covered by the build); a site's custom component built for 4.x
  (a compile break by design).
- **Release notes (D18):**
  - New: content finders. Implement `IContentFinder` to decide which page an address shows when no page has its
    URL (Docs/FrontEnd.md "Content finders"). `EntryPage` cascades the `EntryModel` (now with `ContentFinder` and
    `RouteValues`) to views and layouts.
  - **Breaking:** `ICustomContentComponent.Content` is replaced by `Guid ContentId`. A custom component declares
    `[Parameter] public Guid ContentId { get; set; }` instead of `Content` and loads the page when it needs it with
    `IContentService.GetContentAsync(new GetContentParameters { Id = ContentId, Cached = true })`.
  - **Breaking:** a class that implemented the 4.x `IContentFinder` (`TryFindContent(HttpContext)`, which never
    ran, D52) does not compile against the new interface of the same name.
  - Obsolete: `ContentExtensions.Url(this Content)` → read `Content.Url` (the stored form, `blog`) or
    `ContentUrl.SiteRelative(content)` for the link (`/blog`). `Content.Url` is no longer obsolete or hidden from
    IntelliSense; code that suppressed CS0618 for it can drop the pragma.
  - `ContentService` and `MembershipService` are partial classes in several files; their constructors and members
    are unchanged (a subclass or decorator compiles as before).
  - No schema change.

#### Phase 11 audit — DONE (2026-11-27)
`/audit changed` over the whole of Phase 11 (2f4dc41..773505c, 256 files): deterministic checks, three unit reviewers
(front-end path, bootstrap and start-up, public surface), two independent diff reviewers given only the code
(11a–11d, which also covered CI, release and packaging, and 11e–11g), and two verifiers that tried to disprove every
Medium and every finding to be fixed (eight reproduced with throwaway tests). Nothing Critical or High from Phase 11;
the one High (ZA-187) is older and was downgraded to Medium on verification. Every type Phase 11 made internal,
moved or removed was already in its release notes.
- **Fixed (Medium):** ZA-169 a request under nested domains (`example.com`, `example.com/fr`) was on both for
  redirects, so the shorter domain's redirect could win and loops across the two were saved; now it is on the longest
  one only, as for content (D42), and `RedirectRules.FindLoop` (new overload with the site's domains, used by
  `SaveRedirectAsync`) follows the chain per host. ZA-170 a domain entered with a scheme, a port, a trailing dot or
  slash matched nothing (the domain dialog offers `example.com:8080`), and since 11a its redirects never fired; one
  internal normaliser at match time (stored rows unchanged), a domain with a port matches only that port. ZA-171 a
  page served through a content finder declared its own stored URL as canonical and `og:url`; now the address asked
  for, without its query string. ZA-176 a failing setting at start-up lost the collected warnings and reached only the
  console; and a plugin built against 4.x whose public type implements a moved contract made discovery and Blazor's
  router throw `TypeLoadException` naming neither: the warnings are written first, a failing setting is logged as
  critical, and such an assembly stops the start naming it and the type (one whose unloadable types are all
  non-public still only warns). ZA-177 `release.ps1` had a literal TAB in the template site's path and stopped half
  stamped. ZA-178 a rerun of an older CI run after a newer release would publish older code under a newer version;
  the publish job now requires the newest version tag in HEAD's history (a rerun of the tagged commit is unchanged;
  checked in a scratch repository with a bare origin, 9 cases). ZA-185 the antiforgery attributes answer 500 without
  view services (11e): documented by the audit, then fixed in "Phase 11 audit follow-ups" (D60).
- **Fixed (Low):** ZA-172 content finders that implement `IDisposable`/`IAsyncDisposable` are disposed after the
  lookup; ZA-173 a lookup made from inside a finder does not start the finders again (it recursed without end), and
  `EntryPage` passes the request's token; ZA-174 a finder language that is not a culture is ignored with one warning
  (it logged an error per request), and the docs say the finder's language wins; ZA-175 the cached domain list (now
  read per request by redirects and per miss by finders) uses one key, so a hit builds no DbContext; ZA-179 the
  `AdminDefaultLanguage` error gives the runtime's reason and names invariant-globalization mode; ZA-180 NU1900 (the
  vulnerability data unreadable, offline) is not a build error, the CI scan still fails; ZA-181 the template smoke
  test prints the site's stderr and log on a failure; ZA-182 FluentValidation's `IValidator<T>` is in
  `ExtensionManager.DiscoveryInterfaces`, so a non-public validator is a start-up warning; ZA-183
  `NoCacheInvalidationBus` and `RedisCacheInvalidationBus` are public (D53: defaults of public service interfaces);
  ZA-184 the comment on the content-type import said it could not be left half done (two saves); ZA-186 docs:
  AGENTS.md said there is no content-finder pipeline, the audit runbook pointed at `Core/Modal`, and the 11d/11e/11f
  release notes missed the list layering, the blank provider, the antiforgery attributes and the replacement for
  `AssemblyManager.Assemblies` (corrected in place above).
- **Recorded, older than Phase 11:** ZA-187, ZA-189, ZA-190, ZA-193 (10f); ZA-188 (10g); ZA-194, ZA-197 (10i);
  ZA-191, ZA-192, ZA-195, ZA-196, ZA-198 (12). Owner questions: ZA-185, ZA-198, answered and built in "Phase 11
  audit follow-ups" (D60, D61).
- Tests (43 new cases): `Seo/RedirectDomainTests` (ZA169, ZA170), `Seo/SeoTagsTests` (ZA171: `RenderSeoTags`
  rendered with a finder's `EntryModel` and without), `Content/ContentFinderTests` (ZA172–ZA175),
  `Bootstrap/StartupDiagnosticsTests` (ZA176 with a plugin assembly built at run time against a missing contract,
  ZA179, ZA182), `Packaging/BuildBaselineTests` (ZA177, ZA178, ZA180), the public types snapshot (ZA183). Each new
  test was seen failing with its fix reverted, except the controls (a non-public unloadable type still starts).
  Full suite 1165 (all pass, none skipped) on SQLite, PostgreSQL, SQL Server LocalDB and Redis; Release build 0
  warnings; the template smoke test passes (Windows PowerShell 5.1).
- **Not verified:** a real 4.x plugin package on v5 (the mechanism is reproduced with a run-time-built assembly);
  `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1` on a real site; the smoke test under PowerShell 7 locally (CI runs it);
  the canonical tag and the domain dialog in a browser.
- **Release notes (D18):**
  - Nested domains (`example.com` and `example.com/fr`): a request is on the longest matching domain only, as for
    content, so a redirect for `example.com` no longer applies under `example.com/fr`. The loop check follows each
    host as requests do (a pair of redirects it refused before may now save, and a real loop across nested domains
    is refused).
  - Culture domains entered with `https://`, a trailing dot or slash, or a port now match. A domain with a port
    matches only that port; one without matches any. Before, such a domain matched nothing.
  - `RenderSeoTags`: a page served through a content finder declares the address asked for (no query string) as its
    canonical URL and `og:url`.
  - Content finders that implement `IDisposable`/`IAsyncDisposable` are disposed after each lookup; a lookup from
    inside a finder finds pages by URL only (no finders); a finder language that is not a culture is ignored with
    one warning.
  - A failing start-up setting is logged (critical, `ZauberCMS.Startup`) after the bootstrap's warnings, not only
    written to the console. A site or plugin assembly whose public types cannot load (a plugin built against 4.x
    that implements `IContentProperty` or `ISectionNavGroupAction`) stops the start with a message naming it and the
    type; rebuild it against v5.
  - The `AdminDefaultLanguage` start-up error includes the runtime's reason and says when the site runs in
    globalization-invariant mode.
  - A FluentValidation validator in a site or plugin that is not public (so it never runs) is a start-up warning.
  - `NoCacheInvalidationBus` and `RedisCacheInvalidationBus` are public (sealed), so a site can wrap the default.
  - Breaking: none.

#### Phase 11 audit follow-ups — DONE (2026-11-27)
The owner answered the audit's two questions (2026-11-27): ZA-185 "Register them", ZA-198 "Zauber:Jobs setting".
- **Antiforgery attributes on site controllers (ZA-185, D60).** `AddZauberCms` calls `AddMvcCore().AddViews()` after
  `AddControllers()`: registrations only (the antiforgery filters, TempData, view components, HTML helpers); no Razor
  view engine, and nothing of the MVC rendering path (D52) comes back. `AuthController` keeps its own `IAntiforgery`
  check (its comment no longer says the attribute cannot work). The "documented only" wording the audit wrote (11e
  release notes, FrontEnd.md, AGENTS.md) is replaced by what is registered now.
- **`Zauber:Jobs` (ZA-198, D61).** `ZauberSettings.Jobs` (`JobsSettings`): `DailyCleanup` and `SitemapGenerator`,
  both `true` by default, in the three shipped appsettings files. Checked for other hosted work: the only other
  `IHostedService` is the Redis cache-clear listener (`CacheInvalidationStartup`, needed on every server), and
  `AdminCultureCircuitHandler` is a circuit handler, so neither has a switch. `DailyJob` and
  `SitemapGeneratorService` read the switch from `IOptions<ZauberSettings>` when they start and, when off, log one
  line and return. Documented in [Services.md](Services.md) "Background jobs", AGENTS.md "Configuration",
  Installer.md.
- Tests (10 new cases): `Bootstrap/SiteControllerAntiforgeryTests` (ZA185 ×2, over HTTP on Kestrel with a test
  controller as a site writes one: `[ValidateAntiForgeryToken]` 400 without a token and 200 with one from
  `IAntiforgery`; `[AutoValidateAntiforgeryToken]` 400/200 on a POST, a GET unchecked, `[IgnoreAntiforgeryToken]`
  200 without a token); `Bootstrap/BackgroundJobsTests` (ZA198 ×8: defaults on; the hosted services ZauberCMS
  registers are exactly the two jobs; each job off finishes at once on start; each job on by default or `true`
  keeps running until stopped). Each was seen failing with its fix reverted (500 for the antiforgery cases; the
  daily job kept running with the switch off). The key-parity test passes with the new keys.
  `JobsSettings` is added to the public types snapshot. Full suite 1175 (all pass, none skipped) on SQLite,
  PostgreSQL, SQL Server LocalDB and Redis; Release build 0 warnings. Dev site (`ZauberCMS.Web`): with both
  switches off it logged the two "off on this server" lines and did not start the sitemap generator, by default the
  generator started; a POST to `/api/auth/logout` without a token answered 400 and the home page 200.
- **Not verified:** a real web farm (two servers on one database); the template smoke test locally (CI runs it).
- **Release notes (D18):**
  - Controllers with `[ValidateAntiForgeryToken]`, `[AutoValidateAntiforgeryToken]` or `[IgnoreAntiforgeryToken]`
    work again (since 11e they answered 500); ZauberCMS registers MVC's view features (TempData, view components,
    HTML helpers), not the Razor view engine.
  - New settings `Zauber:Jobs:DailyCleanup` and `Zauber:Jobs:SitemapGenerator` (default `true`, as before): set one
    to `false` on a server that should not run that job, for example every server of a web farm but one for the
    daily clean-up.
  - Breaking: none.

### Phase 12 — .NET 11 (D4)
From the Phase 11 audit, to do with the release planning: **ZA-191** (`GetContentFromRequestParameters.IgnoreInternalRedirect`
is public and never read: honour or obsolete it), **ZA-192** (settings set in code with `Configure<ZauberSettings>` do
not reach the bootstrap's copy, which configures ImageResize, Identity, rate limits and Redis: document or read the
options), **ZA-195** (CI's commit-back of the stamped version conflicts when two pushes to `main-static` are in flight;
it heals on the next push), **ZA-196** (a blank storage or email provider passes start-up and the first upload or
email throws `NullReferenceException` without naming the setting).
Retarget to `net11.0` (`Directory.Build.props`, and the template's own csprojs), move `AnalysisLevel` to 11.0 (D43) and the `global.json` SDK pin to the 11.0 band, move Microsoft/EF/Npgsql packages to 11.x (`Directory.Packages.props`), Serilog.AspNetCore 11 (ZauberCMS.RTE 4 is already in — it has a net11.0 build), full retest on SQLite, SQL Server, PostgreSQL and
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

- Answered: SVG uploads (D6), sanitiser policy (D7), Phase 6 design (D13–D16), ZA-044 hotfix (D17), v5 (D18), installer (D19–D23),
  the Phase 11 audit's two (2026-11-27): antiforgery attributes, register the view features (D60); background
  jobs, a `Zauber:Jobs` setting (D61).
- Closed 2026-11-27 (the owner asked for nothing to be left parked):
  - From Phase 7, kept as built unless the owner says otherwise: the installer does not restart the site; an Admin
    Email Address is promoted by a social login or by setting the password from an emailed link, not by an opened
    confirmation link; starter content ships in the template only; `IMembershipService.RegisterUserAsync` does not
    check `AllowRegistration` (the built-in page and the external sign-in do, so a site's own form keeps working, as
    Umbraco's member service does not check either).
  - Answered: the installer keeps writing `appsettings.json` (D40); its pages stay fixed (D39, ZA-151 declined).
