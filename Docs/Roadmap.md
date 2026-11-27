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
  **23 open findings** (0 Critical, 0 High, 7 Medium, 16 Low), each one assigned below to Phase 11 or the
  [Backlog](#backlog-no-phase-yet). **Next: Phase 11** (engineering baseline, API clean-up, v5; design questions
  inside a phase are decided Umbraco's way and recorded, as the owner delegated).
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
| D27 | **Services do no authorisation; access is checked where a call comes in** (the page, component or endpoint), as in Umbraco and WordPress. Replaces D10: the opt-in permission hook was weighed in Phase 10a and dropped (no shipped path needs it, it does nothing against plugin code, and it would put a rule on every write method). What Phase 10a does instead: `ICurrentUserAccessor`, so services can be called outside a circuit (ZA-093). Back-office roles other than Admin go to the Backlog; that is where service checks would be looked at again. (owner, 2026-11-26) | ZA-140, ZA-093 |
| D28 | **Save hooks see every row a change writes, and one veto stops the whole change.** `SaveChangesAndLog` runs `IBeforeEntitySave<T>` for every row the save writes, of every entity type (the change tracker), each with its own state, then saves once, then runs `IAfterEntitySave<T>` for the same rows. A move, recycle, restore, permanent delete or emptied bin is one change: any `false` from a before-save hook and nothing of it is written (the transaction rolls back). Umbraco does the same: cancelling `Moving`, `Deleting` or `EmptyingRecycleBin` cancels the whole operation, and the "-ed" notifications list every item moved or deleted. Our hooks are per row rather than per operation, so they also see the items below (Umbraco's "-ing" notifications list only the item named). Not hooked: bulk maintenance that runs straight against the database (audit and version retention, orphan clean-up). (owner delegated to Umbraco parity, 2026-11-26, Phase 10b) | ZA-012, ZA-157, ZA-159, ZA-018 |
| D29 | **A "Save only" draft runs the hooks of the rows it writes, like any save: the draft rows, not the live page.** It writes `UnpublishedContent` (the page's and its blocks' drafts), so `IBeforeEntitySave<UnpublishedContent>` runs; the live `Content` row only gets its link to the draft (Modified), and a new block's row, which only the draft reaches, is Added. A plugin that mirrors the live site (a search index) watches `Content`; one that wants drafts too watches `UnpublishedContent`. Umbraco separates the same way: Save raises `Saving`/`Saved` and Publish raises `Publishing`/`Published` as well; its external index holds published content only. Tag links follow the live values, as Umbraco's tags do: a draft leaves them, a publish, version restore or copy writes them, a permanent delete removes them. (owner delegated to Umbraco parity, 2026-11-26, Phase 10b) | ZA-159, ZA-099 |
| D30 | **A picker stores one shape for any count: a JSON array of ids in the order the editor chose, even for one item; older values are read, not rewritten.** The content and media pickers wrote one item as a bare id and several as a JSON array. They now always write `["id", …]`, and everything that reads a picker value (`ToPickerIds`, `GetContentItems`, `GetMediaItems`, `GetUsers`, `GetValue<Guid>` and `GetValue<List<Guid>>`, the admin's single-id fields) takes both shapes. There is no data migration: values live in pages, drafts, versions and block snapshots across three providers, and a value becomes the new shape the next time its page is saved. Umbraco does the same: Media Picker 3 stores a JSON array whatever the maximum, and its value converters return one item or a list from the property's settings, not from the stored shape; when a stored format changed, its converters kept reading the old one. The read side returns the stored order, as Umbraco's converters do. (owner delegated to Umbraco parity, 2026-11-26, Phase 10c) | ZA-029, ZA-078 |
| D31 | **Stored values do not depend on the culture: numbers and dates are written in the invariant culture, and property aliases are unique on a page.** The numeric editor writes `1.5` and the date editor `2026-11-26T14:30:00` (with the time, which the long-date text dropped); values written in the admin's culture before this still read, the invariant form first. A content type is refused when two of its properties share an alias, ignoring case, on the type itself or through a composition (from either side); values that already repeat an alias read the first instead of throwing. Umbraco stores decimals and dates invariant, and refuses a document type or composition whose aliases clash. (owner delegated to Umbraco parity, 2026-11-26, Phase 10c) | ZA-027, ZA-028 |
| D32 | **An error in one part of the admin is shown in that part, with "Try again"; the circuit and the rest of the page stay.** `AdminErrorBoundary` sits round each property editor, each dashboard tab, the sidebar navigation and the page body (cleared on navigation); each side panel's content has one too, with "Close", and so do Radzen's dialogs (round `<RadzenComponents />`). It says what could not be shown, logs the exception, and shows the exception's message only when Blazor's detailed errors are on (`CircuitOptions.DetailedErrors`), otherwise "The details are in the site's log". An editor that failed keeps its last value, so a save does not lose it. Umbraco does the same: a property editor or extension that fails shows an error in its place and the rest of the document stays editable, and server errors show their details to the back office only in debug mode. Retry is offered because a failure is often passing (a timeout, a missing service a reload brings back); Umbraco's answer is a reload, which here would lose unsaved work. (owner delegated to Umbraco parity, 2026-11-27, Phase 10d) | ZA-097 |
| D33 | **Front-end errors get an error page outside Development: the site's own page at `Zauber:ErrorPagePath` (default `/error`), else a plain built-in page.** Setup adds ASP.NET's exception handler for that address, so editors can make the error page a content page at `/error`, or a site can point the setting at a page of its own; when there is no page there, or it fails as well, a built-in page (no database, no layout, the request id) is sent with status 500. Empty turns it off for a site that sets up its own handler. Umbraco also relies on ASP.NET's exception handler and lets the site provide the page. (owner, 2026-11-27, Phase 10e) | ZA-098 |
| D34 | **A redirect says whether its From URL is a regular expression (`IsRegex`, a nullable column); otherwise it matches exactly.** Rows saved before the column existed are null and keep the old guess (regex characters mean regex) until they are saved again, so no site changes behaviour on upgrade. A save is refused when the redirect would send an address to itself or close a circle with the saved ones, and the middleware never redirects an address to itself. (owner, 2026-11-27, Phase 10e) | ZA-116 |
| D35 | **Deleting a user erases the person; there is no user recycle bin.** The row stays, marked deleted, so pages and versions still point at an author, but its user name becomes `deleted-<id>` and its email, phone, password, profile values, logins, claims and roles go; the address and the old name can register again. Audit rows keep their text until the daily job removes them, as the record of who did what. Umbraco also deletes members for good. Users deleted before v5 keep their data (see Open questions). (owner, 2026-11-27, Phase 10e) | ZA-033 |
| D36 | **Image resizing does not upscale, and the resize cache is capped at 2 GB, by default.** `Zauber:ImageResize:AllowUpscale` false (a bigger size than the original returns the original size) and `Cache:MaxCacheBytes` 2147483648 (the oldest copies are removed as new ones are written). Both can be set back; existing sites notice, so it is in the release notes. (owner, 2026-11-27, Phase 10e) | ZA-104 |

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
  notifications after the scope completes); see the Backlog.

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
  keep their data: see Open questions.
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
  throttle and used tokens are per server; users deleted before this keep their data (Open questions).
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

### Phase 11 — Engineering baseline, API clean-up, v5
ZA-089, ZA-087, ZA-094, ZA-090, ZA-091, ZA-092, ZA-164 (Medium); ZA-109, ZA-118, ZA-119, ZA-120, ZA-121, ZA-123,
ZA-124, ZA-125, ZA-126, ZA-131, ZA-165 (Low).
- **Found in Phase 10e, do first:** ZA-164 (the front end's 404 page is sent with an empty body; reproduce in a bare
  .NET 10 app, then render `Zauber:NotFoundComponent`), ZA-165 (a redirect's domain is ignored).
- **Baseline:** ZA-131 (`Directory.Build.props`, analyzers, central package management, `.editorconfig`,
  `.gitattributes`, an SDK pin, a vulnerability scan in CI); CI and release hardening ZA-089, ZA-109.
- **Packaging and template:** ZA-087 (npm manifests in the Components package), ZA-094 (pinned asset package in the
  template), ZA-118 (un-skips its waiting test), ZA-119 (package metadata), ZA-123 (template rough edges).
- **Start-up and DI:** ZA-120 (discovery and configuration fail silently), ZA-121 (DI leftovers, among them the
  unused `IdentityUserAccessor` that `ICurrentUserAccessor` replaces).
- **The deliberate breaking batch for v5:** internalise non-contract types (ZA-092), move Radzen-typed contracts
  out of Core (ZA-091), remove the unwired MVC path (ZA-090), naming conventions (ZA-124), split the very large
  services and remove `Content.Url` (ZA-125), give interactive custom components an id or DTO instead of the whole
  `Content` (ZA-126).
- The method-scoped DbContexts → `IDbContextFactory` (a known deviation in [Standards.md](Standards.md); no finding
  needs it since ZA-011 was fixed in Phase 10). Do it here or drop it from the deviations list.

### Phase 12 — .NET 11 (D4)
Retarget to `net11.0`, move Microsoft/EF/Npgsql packages to 11.x, Serilog.AspNetCore 11 (ZauberCMS.RTE 4 is already in — it has a net11.0 build), full retest on SQLite, SQL Server, PostgreSQL and
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

## Backlog (no phase yet)

Open findings that are not in a phase: each needs an owner decision, outside work, or is a feature in its own
right. Move one into a phase when that is settled.

| Finding | Why it has no phase |
| --- | --- |
| ZA-128 [Low] Accessibility roll-up (unlabelled icon buttons, clickable divs, inputs without labels) | A pass over every admin screen. Best done once, after Phase 10d's admin changes; the owner decides when. |
| ZA-129 [Low] The admin UI is not localised | A feature: which languages, resource files, who translates. Owner decision. |
| ZA-151 [Low] The installer's pages cannot be restyled or extended from a site | Open question for the owner (below). |
| — Back-office roles other than Admin (an "Editor" who may edit content but not users) | A feature (D27). Hiding screens and actions per role is the start; whether services then check access too is decided then. |
| ZA-163 [Low] After-save hooks run before the commit, so a change that is rolled back after them (a media file that can't be removed) has already been reported to them | Needs an after-commit point in the hook API (Umbraco raises its "-ed" notifications when the scope completes). A new interface or a change to `IAfterEntitySave<T>`'s contract (it can ask for a re-save inside the transaction): owner decision. |
| ZA-155 [Low] Resized copies of a deleted image stay in `_mediacache` | Needs a way to remove one file's entries from the ImageResize package, or a decision to prune that cache on a schedule. |

## Open questions for the owner

- Answered: SVG uploads (D6), sanitiser policy (D7), Phase 6 design (D13–D16), ZA-044 hotfix (D17), v5 (D18), installer (D19–D23).
- From Phase 7, for the owner to confirm or change:
  - The installer does not restart the site (see Phase 7). Say so if a restart is wanted anyway.
  - An Admin Email Address is promoted by a social login or by setting the password from an emailed link, not by
    an opened confirmation link (see Phase 7, "Different from D22"). The alternative is to promote social logins
    only.
  - Starter content ships in the template only. A built-in kit inside the packages would need views that sites
    cannot edit.
  - Should `IMembershipService.RegisterUserAsync` itself refuse when registration is off? Today only the built-in
    page and the external sign-in paths do, so a site's own registration form keeps working.
  - Should the installer write the database settings to a file of its own (for example `appsettings.Zauber.json`)
    that a publish does not overwrite? It would close the common case behind D24; it changes D19's "into
    `appsettings.json`" and does not survive deployments that delete extra files.
  - The installer's pages cannot be restyled or given extra steps from a site (ZA-151). Wanted?
- From Phase 10e:
  - Users deleted before v5 still hold their personal data (email, phone, password hash), keep their address from
    being registered again, and can still be brought back with `RecoverAdminUser`. Erase them on upgrade (a one-off
    step at start-up: GDPR-clean, but it cannot be undone), or leave them as they are? The phase left them alone
    because the erase is a one-way change to existing data.
