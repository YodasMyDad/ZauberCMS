# Rework 2026 — Roadmap & Handoff

**Start here when resuming the rework.** Read order: [AGENTS.md](../AGENTS.md) → this file →
[.audit/report.md](../.audit/report.md) (findings by ID) → the Docs file for the area you're touching.

## Status (2026-11-25)

- Branch **`rework-2026`** (pushed). All rework happens here. **Do not merge or push to `main-static`** until the
  owner says so — every push there publishes to nuget.org.
- Done: docs consolidated and fact-checked; first full audit run (IDs now `ZA-001`…`ZA-143`).
- **Phase 0 done** (ZA-044), **Phase 1 done** (ZA-102, ZA-130, ZA-133: all packages on their latest .NET 10
  versions), **Phase 2 done** (test projects + CI test step), **Phase 3 done** (broken admin actions) and **Phase 4 done**
  (access control & identity). After Phase 4 the owner asked for three follow-ups, also done (see "After Phase 4" below).
  **Phase 5 done** (content integrity).
  **102 open findings** (0 Critical, 14 High, 48 Medium, 40 Low). **Next: Phase 6.**
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
| D6 | **SVG uploads are sanitised on upload** (scripts, event handlers, external references removed) and served with a restrictive Content-Security-Policy. | ZA-005 |
| D7 | **Sanitiser: `<iframe>` only from a configurable host allow-list** (default YouTube, Vimeo); `data:` URLs only for images. | ZA-021, ZA-134 |
| D8 | External sign-up (ZA-002): fix with unit tests now; the owner tests against a real provider later. Until then it is "not verified live". | ZA-002 |
| D9 | Fix the duplicated `<RadzenComponents>` (ZA-138) in Phase 4. | ZA-138 |
| D10 | **Service authorisation is an opt-in permission hook, done as its own later phase** (Phase 10a): services ask a replaceable permission service before a write; the default allows everything, a site can switch on a strict mode. A strict default is a question for the major version. | ZA-140 |
| D11 | **Redirects may point at another site** (a full http/https address). | ZA-116 area |
| D12 | **ZauberCMS.RTE 4 is taken now on .NET 10** (it targets net10.0 and net11.0), not at the .NET 11 move. | ZA-134, ZA-136 |
| D13 | **Cache: entity graphs stay in each server's memory cache; Redis only carries invalidation** (pub/sub "clear prefix" plus a version counter, so every server drops stale entries). Nothing secret or half-loaded is stored in Redis; no public API change. Shared DTO caching is not planned. (2026-11-25) | ZA-051, ZA-014, ZA-055, ZA-072 |
| D14 | **PostgreSQL Initial migration:** reproduce on a real PostgreSQL first; if it fails, correct the PostgreSQL `Initial` migration (one-off exception to rule 1, explained in the commit and `Docs/Data.md`), then add the missing ContentVersion indexes for all three providers through `migrations.ps1`, clearing duplicate "current published" flags before the unique index. (2026-11-25) | ZA-050 |
| D15 | **Tests run against real PostgreSQL and Redis containers locally and in CI** on every push; skipped with a clear message when Docker isn't available. SQL Server stays on SQLite-level coverage for now. (2026-11-25) | Phase 6 |
| D16 | **ZA-143 goes at the end of Phase 6**, after the PostgreSQL/Redis work, both halves together. (2026-11-25) | ZA-143 |
| D17 | **No hotfix for ZA-044.** The fix ships with the v5 release when the owner is ready; 4.1.3 stays as published until then. (2026-11-25) | ZA-044 |
| D18 | **A v5 major is acceptable, together with .NET 11** (Phases 11 + 12 in one release). The rework ships as 4.x minors with `[Obsolete]` shims; v5 removes them. (2026-11-25) | Phase 11, 12 |

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

### Phase 6 — PostgreSQL & Redis made real (D1, D13–D16)
- Test infrastructure first (D15): PostgreSQL and Redis containers for the test suite locally and as CI services;
  skipped with a clear message without Docker. Run the app on each.
- PostgreSQL: ZA-050 (per D14: repro, correct `Initial`, new index migration ×3), ZA-007 (unquoted raw SQL),
  ZA-017 (`ToLower` search), ZA-122 (design-time factory file case).
- Redis (D13): redesign `DefaultCacheService` as memory cache for entity graphs + Redis pub/sub and version counter
  for multi-node prefix invalidation. Fixes ZA-051 (restricted pages and password hashes through Redis), ZA-014 +
  ZA-055 (key registry unbounded, lost on restart, race), ZA-072 (cache key ignores includes).
- Then ZA-143 (D16): resolve content only for the content catch-all route, and give a front-end circuit the
  language of its page, together.

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

- Installer scope (Phase 7), including the registration switch / CAPTCHA (ZA-024).
- Answered: SVG uploads (D6), sanitiser policy (D7), Phase 6 design (D13–D16), ZA-044 hotfix (D17), v5 (D18).
