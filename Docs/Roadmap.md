# Rework 2026 — Roadmap & Handoff

**Start here when resuming the rework.** Read order: [AGENTS.md](../AGENTS.md) → this file →
[.audit/report.md](../.audit/report.md) (findings by ID) → the Docs file for the area you're touching.

## Status (2026-11-26)

- Branch **`rework-2026`** (pushed). All rework happens here. **Do not merge or push to `main-static`** until the
  owner says so — every push there publishes to nuget.org.
- Done: docs consolidated and fact-checked; first full audit run (IDs now `ZA-001`…`ZA-160`).
- **Phase 0 done** (ZA-044), **Phase 1 done** (ZA-102, ZA-130, ZA-133: all packages on their latest .NET 10
  versions), **Phase 2 done** (test projects + CI test step), **Phase 3 done** (broken admin actions) and **Phase 4 done**
  (access control & identity). After Phase 4 the owner asked for three follow-ups, also done (see "After Phase 4" below).
  **Phase 5 done** (content integrity). **Phase 6 done** (PostgreSQL & Redis). **Phase 7 done** (installer,
  2026-11-26). **Phase 8 done** (media recycle bin, 2026-11-26). **Phase 9 done** (Block List redesign, 2026-11-26).
  **Phase 10 in progress** (performance & async): steps A, B and C done.
  **71 open findings** (0 Critical, 0 High, 23 Medium, 48 Low). **Next: Phase 10 step D** (ZA-080, ZA-086, ZA-011).
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
| D10 | **Service authorisation is an opt-in permission hook, done as its own later phase** (Phase 10a): services ask a replaceable permission service before a write; the default allows everything, a site can switch on a strict mode. A strict default is a question for the major version. | ZA-140 |
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

## Working rules for the rework

1. One phase (or one finding group) per change; keep diffs reviewable. **Commit to `rework-2026` at the end of each
   phase** (owner instruction 2026-11-25), once it builds and is verified; push only when asked. Never commit
   `ZauberCMS.Web/app.db`.
2. Every fix: follow [Standards.md](Standards.md), grep all callers of anything whose signature/default changes,
   `dotnet build ZauberCMS.sln -c Release` clean, and state how it was verified (run the app for UI behaviour).
3. After fixing, update `.audit/report.md` (move the finding to "Fixed", keep the ID) and any doc the change makes
   stale. Re-run `/audit changed` at the end of each phase.
4. Schema changes: three migrations via `migrations.ps1` — and from Phase 6 on, verify on PostgreSQL too.
5. Public API (D18): everything ships as v5. Prefer additive changes and `[Obsolete]` where cheap; a break is allowed
   when it buys something, and goes in the phase's release notes ("Breaking:"). The big deliberate batch stays in
   Phase 11.
6. Add a regression test with each fix. Where a skipped test is already waiting for the finding
   (`Skip = "ZA-NNN (open)…"`), remove the skip in the same change.

## Phases (in order)

### Phase 0 — Critical hotfix — DONE 2026-11-25
- **ZA-044** fixed: `[Authorize(Roles = Admin)]` added to the three `/admin/seo/*` pages. Verified by running the
  app (anonymous requests redirect to login); details in the audit report.
  ⚠ The hole is still in the **published 4.1.3 packages**: the fix is only on `rework-2026`. Owner decision D17:
  no hotfix; the fix ships with the v5 release.

### Phase 1 — Dependencies to latest on .NET 10 (D4) — DONE 2026-11-25
- Everything is on its latest version that runs on .NET 10 (Radzen 13, MailKit 5, HtmlSanitizer 11, ZauberCMS.RTE
  3.0.0, Microsoft.* 10.0.15, …). Build has 0 warnings; the vulnerability scan is clean. What was and was not
  tested is listed under ZA-130 in the audit report's "Fixed" section — read it before relying on an untested path.
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
  Details and how each was verified: "Fixed" section of the audit report.
- Public API additions (no breaks): `DeleteUserParameters.UserId` is `[Obsolete]` (use `Id`; still honoured);
  new `ConfirmEmailParameters.NewEmailAddress` and `DataGridParameters.IncludeDeleted`. Behaviour change for
  the release notes: `GetDataGridAsync` no longer returns soft-deleted Content/Media/User rows by default.
- Found, not fixed: ZA-138 (`SectionLayout` renders `<RadzenComponents>` twice, so every notification and dialog
  is doubled). Small; fix it whenever the owner wants.
- Not verified: a real confirmation email round trip; a forced context-menu failure in the browser.

### Phase 4 — Access control & identity — DONE 2026-11-25
- Fixed: ZA-053, ZA-052, ZA-059, ZA-002, ZA-020, ZA-019, ZA-003, ZA-060, ZA-132 (route half), ZA-021, ZA-022,
  ZA-005, ZA-138, and ZA-139 (new: content grids refused every filter). What each fix does and how it was
  verified: "Fixed" section of the audit report.
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
  ZA-079, ZA-016, ZA-135 (rich-text content links). What each fix does and how it was verified: "Fixed" section of
  the audit report. Build 0 warnings; 315 tests (312 pass, 3 skipped as before). `/audit changed` found six more
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
  circuits in the root's language). Details: "Fixed" section of the audit report.
  `/audit changed` (two independent reviewers) found 14 more problems in the change: 12 fixed (cache index races,
  Redis resync and counter reset, failed publishes, PostgreSQL test connections, the upgrade keeping the wrong
  version, the remaining culture-dependent `ToLower` calls, path base, filtered-include keys, clears after commit),
  two logged as ZA-146 and ZA-147 (Low). ZA-147 was then checked in the browser with the sample site and is not a
  bug (see the audit report).
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
  content language since the ZA-143 fix). Details: "Fixed" section of the audit report; how it works:
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
  first time). Details: "Fixed" section of the audit report; how it works: [Services.md](Services.md) "Deletes".
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
- Tests: see the audit report header for the count. New: `tests/ZauberCMS.Core.Tests/Media/MediaRecycleBinTests.cs`;
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

### Phase 10 — Performance & async
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
  - The front-end page lookup is cached 5 minutes instead of 5 seconds; changes made straight in the database (not
    through the services) show after that.
  - Step C: `ToPaginatedList`, the `PaginatedList(IQueryable…)` constructor and `BuildPath` are obsolete (use
    `ToPaginatedListAsync`, `BuildPathAsync`). New: `GetContentTreeLevelAsync`, `GetMediaTreeLevelAsync`,
    `IDataListSource.GetItemsAsync` (a default method: existing sources keep working). Admin tree nodes no longer
    carry property values or children (a custom `ITreeContextMenu` that read them loads the item by id); content
    pickers' trees now list unpublished root pages too. `ContentTree`/`MediaTree` refresh themselves; a section
    that passed `Data` no longer needs to. The list editors load their data source after the first render.
  - `AnyContentAsync` ignores content in the recycle bin. `DiskStorageProvider` reports a generic error message and
    logs the detail. Sitemap `priority` is written in the invariant culture and `lastmod` in UTC.

### Phase 10a — Service authorisation (D10)
ZA-140. Design first, with the owner: a replaceable permission service the Core services ask before a write
(who, which entity, which action), default "allow" so existing sites and front-end code that saves on purpose
keep working, a strict mode a site can switch on, and the way front-end or plugin code declares an intended
write. This is also the base for non-admin back-office roles (editors). Whether strict becomes the default is
a v5 question (Phase 11).

### Phase 11 — Engineering baseline, API clean-up, v5
`Directory.Build.props`, analyzers, `.editorconfig`, `.gitattributes`, CI hardening (ZA-089, ZA-109), packaging
(ZA-087, ZA-094, ZA-118, ZA-119), error handling (ZA-097, ZA-098), then the deliberate breaking batch for v5:
internalise non-contract types (ZA-092), move Radzen-typed contracts out of Core (ZA-091), remove the unwired MVC
path (ZA-090), `ICurrentUserAccessor` (ZA-093), naming conventions (ZA-124). Remaining Mediums/Lows by area.

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
