# ZauberCMS Audit Report

Audited commit: 5668365 · Date: 2026-11-25 · Scope: full · Mode: report
Build: PASS · Warnings: 70 (all NuGet vulnerability advisories; 0 compiler; no previous baseline) · Vulnerable packages: 4 · Outdated majors: 5
After Roadmap Phase 1 (2026-11-25, working tree): Build PASS · Warnings: 0 · Vulnerable packages: 0 · Outdated majors: 1 held on purpose (Serilog.AspNetCore 11, needs .NET 11 packages)

> First full run. All findings come from reading code plus the deterministic checks below; **nothing was run in a
> browser or against PostgreSQL/Redis**, so items marked as needing a repro are reasoned, not observed. Critical and
> most High findings were re-read by the auditor before inclusion. No code was changed (report mode); five wrong
> claims in `AGENTS.md` / `Docs/` found by this run were corrected.

## Summary

| Severity | Open | Fixed this run | Resolved since last run |
| --- | --- | --- | --- |
| Critical | 0 | 1 | 0 |
| High | 28 | 1 | 0 |
| Medium | 66 | 2 | 0 |
| Low | 38 | 0 | 0 |

**Work order and owner decisions: [Docs/Roadmap.md](../Docs/Roadmap.md).** Fixed so far: ZA-044 (Phase 0), ZA-102, ZA-130 and ZA-133 (Phase 1), committed on `rework-2026`. Next free ID: ZA-137.

## Open findings

### ZA-001 [High/High] Recycle-bin Restore and Final Delete silently do nothing (regression)

- Where: `ZauberCMS.Components/ContextMenus/RecycleBinMenus/RestoreContextMenu.cs:37`, `FinalDeleteContentContextMenu.cs:35` · Category: C · Since: `ca91ca1`
- Issue / Trigger: `GetContentAsync` now excludes deleted rows by default; these menus don't pass `IncludeDeleted = true`, get `null`, and return silently. Delete content → Recycle bin → Restore.
- Fix: pass `IncludeDeleted = true`; grep all other recycle-bin callers. · Blocked by: nothing (verify in app).
- Reconciled 2026-11-25: same regression also stops recycle-bin items being opened (`RecycleBinTree.razor:82` → `EditContent.razor:42` → `ContentEditor.razor:400` shows "Unable to load").

### ZA-002 [High/High] External (OAuth) sign-up likely always refused (regression)

- Where: `ZauberCMS.Core/Membership/ZauberSignInManager.cs:72-86` · Category: D · Since: `ca91ca1`
- Issue / Trigger: requires an `email_verified` claim, but no provider maps it (`ClaimActions.MapJsonKey`); stock Google/Facebook/Microsoft handlers don't emit it.
- Fix: map the claim per provider (Google has `email_verified`; others need a policy). · Blocked by: identity design decision; verify with a real provider.
- Reconciled 2026-11-25: no `email_verified` claim mapping exists anywhere (providers in `Membership/Authentication/*.cs` set only id + secret), so new external sign-up should fail for Google, Facebook and Microsoft; existing linked accounts still sign in. Needs one live test.

### ZA-003 [High/High] Host-header injection in password-reset/confirm email links

- Where: `ZauberCMS.Core/Extensions/HttpContextExtensions.cs:22`; callers `MembershipService.cs:910`, `Email/Services/EmailService.cs:48`; `AllowedHosts: "*"` in all three appsettings · Category: D
- Fix: configured canonical site URL setting; restrict `AllowedHosts`. · Blocked by: new config key (all three appsettings).

### ZA-004 [High/High] First registered user becomes Admin (re-opens if all users soft-deleted)

- Where: `ZauberCMS.Core/Extensions/RoleExtensions.cs:31` · Category: D
- Fix: install-time setup flow / explicit bootstrap token. · Blocked by: product decision.
- Owner decision 2026-11-25: replace with an explicit setup step — a polished installer (Roadmap Phase 7). No longer blocked.

### ZA-005 [High/Medium] SVG uploads served same-origin unsanitised when opted in

- Where: `ZauberCMS.Core/Settings/GlobalSettings.cs:11-15`, `Providers/DiskStorageProvider.cs:82` · Category: D
- Fix: sanitise SVG or serve with `Content-Disposition`/CSP sandbox. · Blocked by: product decision.

### ZA-006 [High/Medium] Media recycle bin has no UI — soft-deleted media can't be restored or purged

- Where: `ZauberCMS.Core/Media/Services/MediaService.cs:200-214`, `ZauberCMS.Components/ContextMenus/MediaMenus/DeleteMediaContextMenu.cs:47` · Category: C
- Fix: media recycle-bin section or hard-delete with confirmation. · Blocked by: UX decision.
- Reconciled 2026-11-25: the hard-delete branch is unreachable from the UI, so media files are never removed from disk.
- Owner decision 2026-11-25: build a media recycle bin (restore + permanent delete; deleting from the bin removes row and file) (Roadmap Phase 8).

### ZA-007 [High/Medium] Raw SQL with unquoted identifiers likely fails on PostgreSQL

- Where: `ZauberCMS.Core/Extensions/DbContextExtensions.cs:25-53` (`WherePathLike`, `WhereHasCompositionsUsing`); used by copy (`ContentService.cs:819`) and restrict-access · Category: B/C
- Fix: quote identifiers per provider or replace with LINQ. · Blocked by: needs PostgreSQL verification.
- Owner decision 2026-11-25: PostgreSQL is a supported provider — must be fixed and verified on a real PostgreSQL instance (Roadmap Phase 6).

### ZA-019 [High/High] Admin SSRF via SEO checker

- Where: `ZauberCMS.Components/Seo/SeoChecker.razor:53-56`, `Seo/Checks/ResponseStatusCheck.cs:23`, `BrokenLinksCheck.cs:61` · Category: D · Blocked by: allow-list policy.
- Reconciled 2026-11-25: raised to High — reachable **without login** via ZA-044; also blocks the circuit (sync HTTP, no timeout).
- Update 2026-11-25: ZA-044 fixed, so this is admin-only again. Still open; re-rate severity at the next audit.

### ZA-020 [High/High] Dynamic LINQ from browser strings without allow-list

- Where: `ZauberCMS.Core/Data/Services/DataService.cs:137,143`, `Languages/Services/LanguageService.cs:402,408` · Category: D
- Reconciled 2026-11-25: raised to High — the `DataService` path is reachable anonymously through the SEO redirects grid (ZA-044).
- Update 2026-11-25: ZA-044 fixed, so this is admin-only again. Still open; re-rate severity at the next audit.

### ZA-034 [High/High] Block list: Cancel in a nested modal does not discard its changes (data loss)

- Where: `ZauberCMS.Components/Admin/ContentSection/ContentEditor.razor:595-601`, `ZauberCMS.Components/Editors/BlockListEditorProperty.razor:358-367` · Category: C/E · Since: 2026-11-25 deep-dive
- Issue / Trigger: F1. Changes merge into ancestors immediately; Cancel clears only its own copy. Open B1, delete nested B2, Cancel, save → B2 hard-deleted while B1 still references it; cancelled adds become orphan rows.
- Fix: see [Docs/BlockListEditor.md](../Docs/BlockListEditor.md) §6 (EditSession + SaveContentTreeAsync) or §6.5 stopgaps.
- Reconciled 2026-11-25: confirmed by a second independent read.

### ZA-035 [High/High] Block list: root editor replaces pending changes instead of merging

- Where: `ZauberCMS.Components/Admin/ContentSection/ContentEditor.razor:613-621`, `ZauberCMS.Components/Editors/BlockListEditorProperty.razor:366` · Category: C/E · Since: 2026-11-25 deep-dive
- Issue / Trigger: F2. Two block-list properties on one type (or via composition) → only the last-touched property's changes are saved; duplicate `ChangesPending` call leaves root with only the nested subset.
- Fix: see [Docs/BlockListEditor.md](../Docs/BlockListEditor.md) §6 (EditSession + SaveContentTreeAsync) or §6.5 stopgaps.
- Reconciled 2026-11-25: confirmed by a second independent read.

### ZA-036 [High/High] Block list: nested update dropped when item already in Added

- Where: `ZauberCMS.Components/Editors/Models/BlockListState.cs:196-199` · Category: C/E · Since: 2026-11-25 deep-dive
- Issue / Trigger: F3. Add B1 with new B2, close, reopen B1, edit B2, save → B2 persisted with original values.
- Fix: see [Docs/BlockListEditor.md](../Docs/BlockListEditor.md) §6 (EditSession + SaveContentTreeAsync) or §6.5 stopgaps.
- Reconciled 2026-11-25: confirmed by a second independent read.

### ZA-038 [High/High] Block list: 'Save only' (draft) on published content saves blocks live

- Where: `ZauberCMS.Components/Admin/ContentSection/ContentEditor.razor:623-679`, `ZauberCMS.Core/Content/Services/ContentService.cs:214-270` · Category: C/E · Since: 2026-11-25 deep-dive
- Issue / Trigger: F6. Parent goes to UnpublishedContent but blocks are saved/deleted live; 'Clear changes' can't revert them.
- Fix: see [Docs/BlockListEditor.md](../Docs/BlockListEditor.md) §6 (EditSession + SaveContentTreeAsync) or §6.5 stopgaps.
- Reconciled 2026-11-25: confirmed by a second independent read.

### ZA-040 [High/Medium] Hard delete notifies `NotifyContentDeleted(null)` to every session

- Where: `ZauberCMS.Core/Content/Services/ContentService.cs:578`, `ZauberCMS.Components/Admin/ContentSection/ContentEditor.razor:607` · Category: C/E · Since: 2026-11-25 deep-dive
- Issue / Trigger: F9. Every open content editor in every circuit refreshes (unsaved edits lost), on the publisher's thread.
- Fix: see [Docs/BlockListEditor.md](../Docs/BlockListEditor.md) §6 (EditSession + SaveContentTreeAsync) or §6.5 stopgaps.

### ZA-045 [High/High] Admin "Delete user" never deletes, but reports success

- Where: `ZauberCMS.Components/Admin/UsersSection/UserEditor.razor:232-236`, `ZauberCMS.Core/Membership/Services/MembershipService.cs:366`, `ZauberCMS.Core/Membership/Parameters/DeleteUserParameters.cs:9-10` · Category: C · Since: 2026-11-25
- Issue / Trigger: The editor sets `UserId`, the service reads `Id` (empty) and returns "User not found"; the editor's check is inverted (`if (!Success)`), so it announces the delete and navigates away. The account stays fully active. Users → open user → Delete → Yes.
- Fix: read one property (obsolete the other), fix the condition, show errors. · Blocked by: public parameter class (`[Obsolete]`, don't remove).

### ZA-046 [High/High] Duplicate content URL on language-assigned content breaks every front-end page

- Where: `ZauberCMS.Core/Content/Services/ContentService.cs:1492-1496` · Category: C · Since: 2026-11-25
- Issue / Trigger: `dict.Add(c.Url, …)` throws on a duplicate URL and runs on every request (`:2147`); the middleware swallows it but the `EntryPage.razor:52` fallback rethrows. Duplicates arise from copying a node twice (`:875`, `Url + "-copy"`), manual URL edits (`:285`, no uniqueness check) and restore (`:2261`).
- Fix: `TryAdd`, then enforce URL uniqueness on save, copy and restore. · Blocked by: nothing for `TryAdd`.

### ZA-047 [High/High] Any non-draft save silently discards a pending "Save only" draft

- Where: `ZauberCMS.Core/Content/Services/ContentService.cs:211-212,346-349` · Category: C · Since: 2026-11-25
- Issue / Trigger: `SaveContentAsync` removes the `UnpublishedContent` row whenever `SaveUnpublishedOnly` is false. Sort, Move, Restrict access, Culture & Domains and Restore all save the published row, so sorting a draft page's siblings deletes its draft.
- Fix: only remove the draft on an explicit publish (opt-in flag). · Blocked by: new parameter-class property.

### ZA-048 [High/High] Recycle-binning a parent leaves its children live and unreachable

- Where: `ZauberCMS.Core/Content/Services/ContentService.cs:494-517`, `ZauberCMS.Components/ContextMenus/ContentMenus/DeleteContentContextMenu.cs:36-42` · Category: C · Since: 2026-11-25
- Issue / Trigger: Only the one row gets `Deleted = true` although the dialog says "and its children". Children still resolve by URL and appear in the sitemap, are in neither tree, and block the parent's final delete.
- Fix: cascade soft delete and restore over `Path` descendants in one transaction. · Blocked by: behaviour change; ZA-007 on PostgreSQL.

### ZA-049 [High/Medium] Copy shares the original's draft row; publishing either then throws

- Where: `ZauberCMS.Core/Content/Mapping/ContentMappingExtensions.cs:26`, `ZauberCMS.Core/Content/Services/ContentService.cs:869-895` · Category: C · Since: 2026-11-25
- Issue / Trigger: `MapToNew` copies `UnpublishedContentId` and `CreateCopy` doesn't reset it. Published page with a saved draft → Copy → publish either one: the shared row is deleted while the other still references it.
- Fix: null `UnpublishedContentId` in `CreateCopy` and the block clone (`:734`). · Blocked by: nothing.

### ZA-050 [High/High] PostgreSQL initial migration contains SQL Server filter syntax; two intended indexes exist on no provider

- Where: `ZauberCMS.Core/Content/Mapping/ContentVersionDbMapping.cs:31,50-60`, `ZauberCMS.Core/Data/Migrations/PostgreSql/20250924143438_Initial.cs:744` · Category: B · Since: 2026-11-25
- Issue / Trigger: `HasFilter("[IsLatestDraft] = 1")` is emitted unchanged into the PostgreSQL migration, where it is invalid — a fresh `DatabaseProvider=PostgreSql` install should fail at `Database.Migrate()`. Three `HasIndex(x => x.ContentId)` calls also collapse into one, so `IX_ContentVersion_ContentId` and the unique current-published index were never created. Read, not run against PostgreSQL.
- Fix: named indexes with per-provider filters, new migrations ×3, repair the PostgreSQL initial migration. · Blocked by: schema; the unique index may fail on existing duplicates.
- Owner decision 2026-11-25: PostgreSQL is a supported provider — must be fixed and verified on a real PostgreSQL instance (Roadmap Phase 6).

### ZA-051 [High/Medium] Redis mode serialises entity graphs: role-restricted pages can be served to anonymous users

- Where: `ZauberCMS.Core/Shared/Services/DefaultCacheService.cs:39-54,168-176`, `ZauberCMS.Core/Content/Models/Content.cs:179-189`, `ZauberCMS.Components/Pages/EntryPage.razor:70` · Category: B/D · Since: 2026-11-25
- Issue / Trigger: With `RedisConnectionString` set, a cache hit returns content whose `[JsonIgnore]` navigations (`ContentRoles`, `Children`, `MediaRoles`) are empty, so the EntryPage role check passes. Cached user rows also put password hashes in Redis, and `Dictionary<object,string>` results don't deserialise. Not run.
- Fix: cache DTOs, or keep entity graphs in memory and use Redis only for invalidation. · Blocked by: cached return types are public API.
- Owner decision 2026-11-25: Redis caching is supported — must be fixed and verified against Redis (Roadmap Phase 6).

### ZA-052 [High/Medium] Session survives user deletion and password reset via `refreshsignin`

- Where: `ZauberCMS.Components/Controllers/AuthController.cs:17-23`, `ZauberCMS.Core/Membership/Services/MembershipService.cs:371-376` · Category: D · Since: 2026-11-25
- Issue / Trigger: The GET endpoint re-issues the cookie from the DB user without validating the security stamp, `CanSignIn` or `Deleted`; refreshing more often than the 30-minute default validation interval avoids the stamp check entirely. A removed admin or a stolen cookie keeps a valid admin session.
- Fix: `[Authorize]`, validate stamp and `CanSignIn` first, POST + antiforgery, shorter validation interval, reject `Deleted`. · Blocked by: `UserEditor.razor:219,316` navigate to it by GET.

### ZA-053 [High/High] Restricted media becomes public when recycled, and the check is bypassed by URL case

- Where: `ZauberCMS.Core/Media/Services/MediaService.cs:201-210,282-290,399-402`, `ZauberCMS.Core/Media/Middleware/RestrictedMediaMiddleware.cs:18,66-74` · Category: D · Since: 2026-11-25
- Issue / Trigger: The restricted lookup excludes `Deleted` rows but the admin delete is always soft and leaves the file on disk, so a deleted members-only file is served anonymously. The lookup dictionary is also case-sensitive, so a differently-cased path misses the check on Windows/macOS file systems.
- Fix: drop the `Deleted` filter (or 404 deleted media) and use an `OrdinalIgnoreCase` comparer on a normalised path. · Blocked by: nothing.

### ZA-054 [High/High] Admin trees block the circuit with sync-over-async (latent deadlock)

- Where: `ZauberCMS.Components/Trees/ContentTree.razor:89-96,127`, `ZauberCMS.Components/Trees/MediaTree.razor:117-123,167`, `ZauberCMS.Components/Trees/ContentTypeTree.razor:73-79,117`, `ZauberCMS.Components/Admin/StructureSection/Navigation/CompositionsTree.razor:78,101`, `ZauberCMS.Components/DataSources/ContentTypesDataSource.cs:17-22` · Category: A · Since: 2026-11-25
- Issue / Trigger: Expand handlers call `.GetAwaiter().GetResult()` on the renderer context and `HasChildren` does a blocking DB call per node per render. It works only because the paged query methods are synchronous today; making `PaginatedList` async (the stated target) would deadlock every tree.
- Fix: `await` in handlers, batch child flags into `TreeState`, add async `IDataListSource.GetItemsAsync`. · Blocked by: public interface (add + obsolete); must land with the paging refactor.

### ZA-055 [High/Medium] Cache key registry race leaves pages stale until restart

- Where: `ZauberCMS.Core/Shared/Services/DefaultCacheService.cs:58-75,110-127,144-161` · Category: A · Since: 2026-11-25
- Issue / Trigger: On two concurrent cache misses the replaced entry's eviction callback removes the key from the registry after the second caller added it; the entry stays cached but prefix invalidation no longer sees it, and sliding expiry keeps a hot key alive.
- Fix: ignore `EvictionReason.Replaced`; better, per-prefix change tokens plus a per-key lock (also stops stampedes). · Blocked by: nothing (internal).

### ZA-056 [High/High] Role queries always load every user in the role

- Where: `ZauberCMS.Core/Membership/Services/MembershipService.cs:425-430,546-549` · Category: B · Since: 2026-11-25
- Issue / Trigger: `GetRoleAsync`/`QueryRolesAsync` unconditionally include `UserRoles → User`: full user rows including password hashes, soft-deleted users included. Opening the Users section on a site with many members loads them all.
- Fix: drop the include; opt-in `IncludeUsers` or a count projection. · Blocked by: callers reading `Role.UserRoles`.

### ZA-057 [High/High] Block lists and single media pickers are uncached on the front-end

- Where: `ZauberCMS.Core/Extensions/ContentExtensions.cs:133-139,218` · Category: B · Since: 2026-11-25
- Issue / Trigger: `GetBlocks` queries without `Cached` — 3 DB round-trips per block list per request, repeated per nested list — and single-media lookups cost one each. Every page with a block list pays this on every request.
- Fix: `Cached = true` on the non-preview path; skip the COUNT for id queries. · Blocked by: admin preview shares the method.

### ZA-058 [High/Medium] Admin layout re-render makes editors reload and drop unsaved edits

- Where: `ZauberCMS.Components/Admin/Layout/SectionLayout.razor:19,90-95`; `ZauberCMS.Components/Admin/StructureSection/ContentTypeEditor.razor:360-408`, `ZauberCMS.Components/Admin/UsersSection/UserEditor.razor:149-160`, `ZauberCMS.Components/Admin/UsersSection/RoleEditor.razor:147-156` · Category: E · Since: 2026-11-25
- Issue / Trigger: `<CascadingValue Value="@this">` is not `IsFixed`, so every layout re-render re-runs `OnParametersSetAsync` on all consumers; these three editors re-fetch their model there with no guard. Edit a content type, toggle the sidebar → edits gone. Read, not reproduced.
- Fix: `IsFixed="true"`; load in `OnInitializedAsync` or guard on a changed id. · Blocked by: nothing.

### ZA-008 [Medium/Medium] Block-list parent version snapshot taken before blocks are saved

- Where: `ContentVersioningService.cs:44-47,635+`; `ContentEditor.razor` Save → `ProcessBlockListChanges` · Category: C
- Issue: parent's version may miss new blocks / hold old block values. Untested. See [Docs/BlockListEditor.md](../Docs/BlockListEditor.md).

### ZA-009 [Medium/High] Block-list save is not atomic

- Where: `ZauberCMS.Components/Admin/ContentSection/ContentEditor.razor:623-720` · Category: C
- Issue: parent then each block saved/deleted in separate calls, no transaction — failure mid-way leaves a partial tree.

### ZA-010 [Medium/High] Block stylesheets leak into the admin page

- Where: `ZauberCMS.Components/Editors/BlockListEditorProperty.razor:14-20` · Category: E
- Issue: stylesheets emitted as normal `<link>` as well as inside the shadow root.

### ZA-011 [Medium/High] Two DbContext instances per scope

- Where: `ZauberCMS.Core/ZauberSetup.cs:169-186` (`AddDbContext` + `AddScoped<IZauberDbContext, …>`) · Category: A/F
- Issue: Identity stores and services don't share tracker/transaction. Listed in Standards → Known deviations.

### ZA-012 [Medium/High] `SaveChangesAndLog` hooks see `Unchanged` after audit save; ContentType veto can't roll back

- Where: `ContentService.cs:369,982-985` · Category: C
- Reconciled 2026-11-25: same pattern on the recycle path (`ContentService.cs:497-498`).

### ZA-013 [Medium/High] Media delete notifies before save

- Where: `ZauberCMS.Core/Media/Services/MediaService.cs:215-216` · Category: C
- Reconciled 2026-11-25: also double notify on soft delete (service + `DeleteMediaContextMenu.cs:51`) and `NotifyMediaSaved` fires even when the save failed (`MediaService.cs:119,137`).

### ZA-014 [Medium/High] Cache key registry unbounded/per-process under Redis

- Where: `ZauberCMS.Core/Shared/Services/DefaultCacheService.cs:15,52` · Category: A/B
- Issue: grows forever; prefix invalidation not multi-node safe. · Blocked by: Redis SCAN/tagging design.
- Reconciled 2026-11-25: also unsafe on a single node — after a restart the registry is empty while Redis entries persist, so prefix clears miss them.
- Owner decision 2026-11-25: Redis caching is supported — fix as part of the cache redesign (Roadmap Phase 6).

### ZA-015 [Medium/High] `AddFluentValidation` never unsubscribes

- Where: `ZauberCMS.Core/Shared/Validation/EditContextFluentValidationExtensions.cs:22,25` · Category: A
- Reconciled 2026-11-25: same file also has unsynchronised static lists and `async` handlers with no try/catch (a throwing validator ends the circuit) — lines 12-13, 107-125.

### ZA-016 [Medium/Medium] Nullable `IsDeleted = null` bypasses soft-delete filter

- Where: `Content/Parameters/QueryContentParameters.cs:19`, `ContentService.cs:2045` · Category: C · Blocked by: public parameter class.

### ZA-017 [Medium/High] `ToLower()` search comparisons (Turkish-I, provider-dependent)

- Where: `ContentService.cs:1023,2080,2240-2244`; `MembershipService.cs:564-565,1029-1030` · Category: B/C

### ZA-018 [Medium/Medium] `SaveTagItemAsync` bypasses `SaveChangesAndLog` hooks

- Where: `ZauberCMS.Core/Tags/Services/TagService.cs:222-224` · Category: C

### ZA-021 [Medium/Medium] Sanitiser allows `iframe` and `data:`; RTE rendered as raw `MarkupString`

- Where: `ZauberCMS.Core/Shared/Services/DefaultHtmlSanitizerService.cs:18,26`, `ZauberCMS.Web/ContentBlocks/RTEBlock.razor:6` · Category: D · Blocked by: product decision.

### ZA-022 [Medium/Medium] Upload check extension-only for text types

- Where: `ZauberCMS.Core/Providers/DiskStorageProvider.cs:55-63,85+` · Category: D

### ZA-023 [Medium/Medium] Promotion via `AdminEmailAddresses` without confirmed email

- Where: `RoleExtensions.cs:32-33`; `SignInRequireConfirmedAccount: false` · Category: D · Blocked by: deliberate feature.

### ZA-024 [Medium/Low] Registration flooding — only the shared "login" limiter, no CAPTCHA

- Where: `ZauberCMS.Components/Account/Pages/Register.razor:11` · Category: D · Blocked by: new dependency.

### ZA-037 [Medium/High] Block list: deleting a block keeps its pending descendants

- Where: `ZauberCMS.Components/Editors/Models/BlockListState.cs:107-142` · Category: C/E · Since: 2026-11-25 deep-dive
- Issue / Trigger: F4. New descendants of a deleted block are inserted as orphans; DB cascade reads stored JSON so never reaches them.
- Fix: see [Docs/BlockListEditor.md](../Docs/BlockListEditor.md) §6 (EditSession + SaveContentTreeAsync) or §6.5 stopgaps.

### ZA-039 [Medium/Medium] Block list: editor state stale after save (needs repro)

- Where: `ZauberCMS.Components/Admin/ContentSection/ContentEditor.razor:681-780` · Category: C/E · Since: 2026-11-25 deep-dive
- Issue / Trigger: F7. Change lists and `DateUpdated` kept after save → second save gives false concurrency errors / failed deletes / re-adds.
- Fix: see [Docs/BlockListEditor.md](../Docs/BlockListEditor.md) §6 (EditSession + SaveContentTreeAsync) or §6.5 stopgaps.

### ZA-041 [Medium/High] Block list: change bubbling is re-entrant and doubles per level

- Where: `ZauberCMS.Components/Editors/BlockListEditorProperty.razor:358-401` · Category: C/E · Since: 2026-11-25 deep-dive
- Issue / Trigger: F10. Change at depth d merged 2^(d-1) times through async void handlers.
- Fix: see [Docs/BlockListEditor.md](../Docs/BlockListEditor.md) §6 (EditSession + SaveContentTreeAsync) or §6.5 stopgaps.

### ZA-042 [Medium/Low] Block list: `OnParametersSetAsync` rebuilds WorkingCopy each time (needs repro)

- Where: `ZauberCMS.Components/Admin/ContentSection/ContentEditor.razor:364-367` · Category: C/E · Since: 2026-11-25 deep-dive
- Issue / Trigger: F12. If stacked modals re-supply parameters, in-modal edits reset and child callbacks point at disposed components.
- Fix: see [Docs/BlockListEditor.md](../Docs/BlockListEditor.md) §6 (EditSession + SaveContentTreeAsync) or §6.5 stopgaps.
- Reconciled 2026-11-25: mechanism confirmed by reading (`ContentEditor.razor:318-381`; Blazored.Modal re-renders every open instance on each show/dismiss). Repro: add a block, choose a type, open the media picker — expect a return to the type chooser.

### ZA-043 [Medium/Medium] Block previews: Blazor-owned nodes moved into shadow root

- Where: `ZauberCMS.Components/ContentComponents/RenderBlock.razor:12-18,50`, `ZauberCMS.Components/wwwroot/js/shadow-dom.js:40-42` · Category: C/E · Since: 2026-11-25 deep-dive
- Issue / Trigger: F13. New nodes may render outside the shadow root (invisible), clicks inside previews don't fire, no isolation without stylesheets. Fix direction: static HtmlRenderer previews.
- Fix: see [Docs/BlockListEditor.md](../Docs/BlockListEditor.md) §6 (EditSession + SaveContentTreeAsync) or §6.5 stopgaps.

### ZA-059 [Medium/High] Lockout is never triggered by failed passwords

- Where: `ZauberCMS.Core/Membership/Services/MembershipService.cs:612,729`, `ZauberCMS.Core/ZauberSetup.cs:151-153` · Category: D · Since: 2026-11-25
- Issue / Trigger: `PasswordSignInAsync(…, lockoutOnFailure: false)` — the configured 6-attempt lockout never counts. Only the per-IP limiter slows guessing.
- Fix: pass `true`. · Blocked by: UX (users can be locked out for 5 minutes).

### ZA-060 [Medium/Medium] Proxy and transport hardening left to the host; IP allow-list only checked while prerendering

- Where: `ZauberCMS.Core/ZauberSetup.cs:220,385-411`, `ZauberCMS.Components/Admin/Layout/SectionLayout.razor:64-70` · Category: D · Since: 2026-11-25
- Issue / Trigger: No forwarded-headers, HSTS, `nosniff` on uploads or data-protection key persistence. Behind a proxy the login limiter is one shared bucket (ten requests lock out all logins) and the admin IP allow-list compares the proxy's address.
- Fix: opt-in forwarded-headers/known-proxies settings; move the IP check to middleware; `X-Content-Type-Options` on the upload folder. · Blocked by: new config keys.

### ZA-061 [Medium/High] Front-end entry cache is 5 seconds, not 5 minutes; key includes host and query string

- Where: `ZauberCMS.Core/Content/Services/ContentService.cs:907-913`, `ZauberCMS.Core/Middleware/CultureMiddleware.cs:40-46` · Category: B · Since: 2026-11-25
- Issue / Trigger: The call passes `(0, 5)` = 5 s sliding; any URL idle longer re-runs 4-5 queries and every distinct query string is its own entry.
- Fix: intended TTL; key on host + path. · Blocked by: nothing.

### ZA-062 [Medium/High] "Cached" content queries still hit the database

- Where: `ZauberCMS.Core/Content/Services/ContentService.cs:2083-2088,179,462` · Category: B · Since: 2026-11-25
- Issue / Trigger: `BuildQuery` does a sync content-type lookup for `ContentTypeAlias` before the cache check, on every request; the cache key is computed even when not caching.
- Fix: cached alias lookup or join; compute the key only when `Cached`. · Blocked by: nothing.

### ZA-063 [Medium/High] Every extension component is instantiated per request and per block

- Where: `ZauberCMS.Components/Pages/EntryPage.razor:44`, `ZauberCMS/Components/App.razor:60`, `ZauberCMS.Components/ContentComponents/RenderBlock.razor:8,45` · Category: B · Since: 2026-11-25
- Issue / Trigger: `GetInstances<T>` builds every view per request, every property editor per request (only needed in admin) and every block view per `RenderBlock`.
- Fix: `GetImplementations<T>` plus a static alias → type map. · Blocked by: nothing.

### ZA-064 [Medium/High] 404s cost two uncached queries each

- Where: `ZauberCMS.Core/Content/Services/ContentService.cs:1414-1419`, `ZauberCMS.Components/Pages/EntryPage.razor:112` · Category: B · Since: 2026-11-25
- Issue / Trigger: `AnyContentAsync` is uncached with no `Deleted` filter; bot traffic to dotted paths skips the middleware and takes the fallback.
- Fix: cache under the Content prefix. · Blocked by: nothing.

### ZA-065 [Medium/High] Unpublished or deleted content served through side paths

- Where: `ZauberCMS.Core/Content/Services/ContentService.cs:2050-2056,2154-2156,2169,2187-2190` · Category: C · Since: 2026-11-25
- Issue / Trigger: Domain-root and internal-redirect targets have no `Published` filter; `Include(Children)` is unfiltered in the entry lookup and in `QueryContentAsync`.
- Fix: add `Published` / `!Deleted` filters (mirror `:1993-1995`). · Blocked by: views relying on unfiltered `Children`.

### ZA-066 [Medium/High] Slug matching depends on the database provider

- Where: `ZauberCMS.Core/Content/Services/ContentService.cs:281-286,2161`, `ZauberCMS.Core/Middleware/CultureMiddleware.cs:38` · Category: C · Since: 2026-11-25
- Issue / Trigger: URLs are lower-cased on save but the request slug is compared raw: `/About-Us` resolves on SQL Server and 404s on SQLite and PostgreSQL. Trailing slash not trimmed.
- Fix: trim and lower-case the slug invariantly before lookup. · Blocked by: nothing.

### ZA-067 [Medium/High] Nested blocks get public URLs and resolve as pages

- Where: `ZauberCMS.Core/Content/Services/ContentService.cs:272-278,2161`, `ZauberCMS.Components/Admin/ContentSection/ContentEditor.razor:353-354` · Category: C · Since: 2026-11-25
- Issue / Trigger: Slugs are generated for block rows and the slug lookup has no `!IsNestedContent`: `/hero` returns 200 with the missing-view page, and later real pages get a `-1` suffix.
- Fix: skip URL generation for nested content; add the filter. · Blocked by: existing block URLs need cleanup.

### ZA-068 [Medium/High] Admin trees over-fetch and silently stop at 100 children

- Where: `ZauberCMS.Components/Trees/ContentTree.razor:43-51,60-69,88-95`, `ZauberCMS.Components/Admin/ContentSection/Navigation/ContentSectionTree.razor:60-68`, `ZauberCMS.Components/Admin/MediaSection/Folder.razor:105` · Category: B · Since: 2026-11-25
- Issue / Trigger: Tree queries load property data, children and roles they never read; children are capped at 100 (media 200, recycle bin 300) with no paging, so later items are unreachable.
- Fix: tree projection query and load-more. · Blocked by: UX; new service method.

### ZA-069 [Medium/High] Content versions grow without bound

- Where: `ZauberCMS.Core/Content/Services/ContentService.cs:254,432`, `ZauberCMS.Core/Content/Services/ContentVersioningService.cs:193-241` · Category: B · Since: 2026-11-25
- Issue / Trigger: Every save — including each block — writes a full snapshot with no retention; the version list is tracked and loads full snapshot JSON.
- Fix: retention setting + daily prune; no-tracking projection. · Blocked by: new config key ×3.

### ZA-070 [Medium/High] Deleting a content type checks only published, non-deleted content and only the first 10 nested items

- Where: `ZauberCMS.Core/Content/Services/ContentService.cs:1098-1153,1217-1224` · Category: C · Since: 2026-11-25
- Issue / Trigger: The in-use check uses query defaults while the FK cascades: drafts and binned items are hard-deleted silently or a raw FK error surfaces after the audit row is written.
- Fix: `IncludeUnpublished = true, IsDeleted = null`, `AnyAsync`. · Blocked by: nothing.

### ZA-071 [Medium/High] Language dictionary load is N+1 and synchronous

- Where: `ZauberCMS.Core/Languages/Services/LanguageService.cs:360-376` · Category: B · Since: 2026-11-25
- Issue / Trigger: Dictionaries are re-queried per language with a linear lookup per key.
- Fix: two `ToListAsync` calls and a lookup. · Blocked by: nothing.

### ZA-072 [Medium/Medium] Cache key ignores collection includes

- Where: `ZauberCMS.Core/Extensions/CacheExtensions.cs:39-44`, `ZauberCMS.Core/Content/Services/ContentService.cs:1989-2002,2028-2056` · Category: B · Since: 2026-11-25
- Issue / Trigger: With split queries `ToQueryString` returns only the first statement, so `IncludeChildren` true/false share a key and whichever is cached first wins.
- Fix: append include flags to the key. · Blocked by: nothing.

### ZA-073 [Medium/Medium] Bulk admin operations run item by item

- Where: `ZauberCMS.Components/ContextMenus/ContentMenus/RestrictAccessContextMenu.cs:94-125`, `ZauberCMS.Core/Content/Services/ContentService.cs:681-683,727-730,828`, `ZauberCMS.Components/Seo/Startup/SitemapGeneratorService.cs:74-78,130-133` · Category: B · Since: 2026-11-25
- Issue / Trigger: Restrict-access does a full save (version + cache flush) per descendant with no transaction; copy queries per block; the sitemap stops at 2000 items.
- Fix: batch. · Blocked by: save-hook behaviour.

### ZA-074 [Medium/High] Draft versions snapshot the live values, not the draft

- Where: `ZauberCMS.Core/Content/Services/ContentVersioningService.cs:44-47,87-97`, `ZauberCMS.Core/Content/Services/ContentService.cs:245-254` · Category: C · Since: 2026-11-25
- Issue / Trigger: `CreateVersionAsync` reloads the DB row and ignores the draft being saved.
- Fix: build the snapshot from `parameters.Content` for drafts. · Blocked by: nothing.

### ZA-075 [Medium/High] Version restore rewrites tree position and publish state, keeps the stale draft

- Where: `ZauberCMS.Core/Content/Services/ContentVersioningService.cs:173,361-375` · Category: C · Since: 2026-11-25
- Issue / Trigger: Restore sets parent, path, sort order, language and published from the snapshot: a moved page jumps back and a draft version unpublishes a live page. Bypasses `SaveChangesAndLog` and audit.
- Fix: restore name and properties only; clear the draft; audit. · Blocked by: UX change.

### ZA-076 [Medium/High] Culture & Domains always ends in a false "modified by another user" error

- Where: `ZauberCMS.Components/Admin/ContentSection/Dialogs/CultureDomains.razor:112,130`, `ZauberCMS.Components/ContextMenus/ContentMenus/CultureDomainsContentContextMenu.cs:61-65` · Category: C · Since: 2026-11-25
- Issue / Trigger: The dialog saves, then the menu re-saves the same stale object.
- Fix: remove the second save. · Blocked by: nothing.

### ZA-077 [Medium/High] Importing a content type over an existing alias orphans all its values

- Where: `ZauberCMS.Core/Content/Services/ContentService.cs:1791-1815,1737,1951` · Category: C · Since: 2026-11-25
- Issue / Trigger: Property and tab IDs are regenerated, so the next save drops unmatched values; content import is lossy too. Public API only — no admin caller.
- Fix: match existing properties by alias and keep IDs. · Blocked by: nothing.

### ZA-078 [Medium/High] Multi-pick order is lost on the front-end

- Where: `ZauberCMS.Core/Extensions/ContentExtensions.cs:208-209,244-262,285-293` · Category: C · Since: 2026-11-25
- Issue / Trigger: Media, content and user pickers return query order, not the editor's order (unlike `GetBlocks`).
- Fix: reorder by stored IDs. · Blocked by: visible order change.

### ZA-079 [Medium/Medium] Move updates only the moved row's path

- Where: `ZauberCMS.Components/ContextMenus/ContentMenus/MoveContentContextMenu.cs:60-86`, `ZauberCMS.Core/Extensions/DbContextExtensions.cs:55-106` · Category: C · Since: 2026-11-25
- Issue / Trigger: Descendant paths go stale, which feeds copy, restrict-access, the sitemap root check and language-by-path.
- Fix: rewrite descendant paths in the same transaction. · Blocked by: ZA-007.

### ZA-080 [Medium/High] AppState handlers run on other users' circuits without marshalling or filtering

- Where: `ZauberCMS.Components/Admin/ContentSection/ContentEditor.razor:603-611`, `ZauberCMS.Components/Admin/ContentSection/ContentVersions.razor:160-199`, `ZauberCMS.Components/Admin/MediaSection/UpdateMedia.razor:36-44` · Category: A · Since: 2026-11-25
- Issue / Trigger: Handlers mutate component state and call navigation off the dispatcher; the versions panel reloads on any content save anywhere.
- Fix: `InvokeAsync`, filter by id. · Blocked by: fix ZA-040 first.

### ZA-081 [Medium/High] Context-menu actions are fire-and-forget

- Where: `ZauberCMS.Components/Trees/BaseTree.razor:130`, `ZauberCMS.Components/Admin/Layout/SectionTrees.razor:74` · Category: A · Since: 2026-11-25
- Issue / Trigger: `_ = MenuAction(...)`: failures in delete, restore, copy or move are never logged or shown.
- Fix: await inside try/catch with log + notification. · Blocked by: nothing.

### ZA-082 [Medium/High] Sync EF calls inside async service methods (~30 sites)

- Where: `ZauberCMS.Core/Content/Services/ContentService.cs:212,224,291-299,487,564,958,961,2261-2269,2282`, `ZauberCMS.Core/Extensions/DbContextExtensions.cs:66-70,89-93`, `ZauberCMS.Core/Media/Services/MediaService.cs:98,187`, `ZauberCMS.Core/Languages/Services/LanguageService.cs:362-375` · Category: A · Since: 2026-11-25
- Issue / Trigger: Every content, media or type save blocks a circuit thread on several round-trips.
- Fix: `…Async` overloads with the token; `BuildPathAsync`. · Blocked by: `BuildPath` is a public extension (obsolete).

### ZA-083 [Medium/High] Daily audit cleanup loads every expired row, on every app start

- Where: `ZauberCMS.Core/Audit/Services/AuditService.cs:120-130`, `ZauberCMS.Core/Jobs/DailyJob.cs:17` · Category: A · Since: 2026-11-25
- Issue / Trigger: `RemoveRange(IQueryable)` materialises and tracks all audits older than 90 days.
- Fix: `ExecuteDeleteAsync`. · Blocked by: nothing.

### ZA-084 [Medium/High] Non-image uploads are buffered twice in memory; raw exception text shown

- Where: `ZauberCMS.Core/Providers/DiskStorageProvider.cs:221-223,232-236` · Category: A · Since: 2026-11-25
- Issue / Trigger: `MemoryStream` then `ToArray()` holds about twice the file size per upload; failures return `ex.Message` to the user unlogged.
- Fix: stream straight to a `FileStream`; log and return a generic message. · Blocked by: nothing.

### ZA-086 [Medium/Medium] UserManager and its DbContext live for the whole circuit

- Where: `ZauberCMS.Core/Content/Services/ContentService.cs:30`, `ZauberCMS.Core/Email/Services/EmailService.cs:15`, `ZauberCMS.Components/Admin/UsersSection/UserEditor.razor:125,177` · Category: A · Since: 2026-11-25
- Issue / Trigger: Stale tracked users and "a second operation was started on this context" under overlapping operations.
- Fix: resolve per operation. · Blocked by: public constructor signature.

### ZA-087 [Medium/High] Components package leaks npm manifests into consumer projects

- Where: `ZauberCMS.Components/ZauberCMS.Components.csproj:73-76` · Category: F · Since: 2026-11-25
- Issue / Trigger: `package.json` and `package-lock.json` ship as content files; the existing exclusion uses a stale machine-specific path.
- Fix: `Content Remove="package*.json"` + `Pack="false"`. · Blocked by: nothing.

### ZA-088 [Medium/High] `DeleteUserAsync` saves on the wrong DbContext and always reports success

- Where: `ZauberCMS.Core/Membership/Services/MembershipService.cs:366-381` · Category: C/F · Since: 2026-11-25
- Issue / Trigger: The user is loaded via `UserManager` but saved on the separate `IZauberDbContext`; `Deleted` only persists as a side effect of the stamp update, whose result is ignored. Concrete symptom of ZA-011.
- Fix: `userManager.UpdateAsync` and check the result. · Blocked by: nothing.

### ZA-089 [Medium/High] CI can publish one version built from two commits

- Where: `.github/workflows/ci.yml:162-188` · Category: F · Since: 2026-11-25
- Issue / Trigger: Packages are pushed in arbitrary order with `--skip-duplicate` and the tag is created afterwards; a partial failure followed by a new commit reuses the version.
- Fix: push in dependency order; reserve the tag first. · Blocked by: nothing (CI only).

### ZA-090 [Medium/High] Unwired MVC rendering path ships as public API

- Where: `ZauberCMS.Core/ZauberSetup.cs:121,239,308-313,423-433`, `ZauberCMS.Routing/ZauberCMS.Routing.csproj:17` · Category: F · Since: 2026-11-25
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

### ZA-093 [Medium/Medium] Write services need a Blazor component scope

- Where: `ZauberCMS.Core/Content/Services/ContentService.cs:29`, `ZauberCMS.Core/Membership/Services/MembershipService.cs:35`, `ZauberCMS.Core/Media/Services/MediaService.cs:30` · Category: F · Since: 2026-11-25
- Issue / Trigger: Services inject `AuthenticationStateProvider` (27 uses), so save or delete can't be called from a controller, API or hosted service.
- Fix: `ICurrentUserAccessor` with an HttpContext fallback. · Blocked by: public constructors.

### ZA-094 [Medium/Medium] Template pins an SDK-internal asset package at 10.0.0

- Where: `ZauberCMS.Template/template/ZauberCMSTemplate.Site/ZauberCMSTemplate.Site.csproj:11-14` · Category: F · Since: 2026-11-25
- Issue / Trigger: `Microsoft.AspNetCore.App.Internal.Assets` is hard-pinned and never stamped by CI, so new sites serve a stale `blazor.web.js`.
- Fix: `<RequiresAspNetWebAssets>true</RequiresAspNetWebAssets>`; template smoke test. · Blocked by: nothing.

### ZA-095 [Medium/High] Admin and reserved-path detection is a bare prefix match

- Where: `ZauberCMS/Components/App.razor:68-77`, `ZauberCMS.Core/Middleware/CultureMiddleware.cs:28-30`, `ZauberCMS.Core/Middleware/RedirectMiddleware.cs:26-28` · Category: E · Since: 2026-11-25
- Issue / Trigger: A page at `/administration` renders as admin (interactive circuit, Radzen assets) for visitors; `/accounting` or `/api-docs` skip culture and redirect handling.
- Fix: match on a segment boundary via one shared helper. · Blocked by: nothing.

### ZA-096 [Medium/Medium] Admin scripts don't load when arriving by enhanced navigation

- Where: `ZauberCMS/Components/App.razor:34-45`, `ZauberCMS.Components/Pages/NewSite.razor:8` · Category: E · Since: 2026-11-25
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
- Issue / Trigger: Double loads and flicker; with ZA-058 some pages load four times.
- Fix: `InteractiveServerRenderMode(prerender: false)` for admin. · Blocked by: the IP allow-list must move to middleware first (ZA-060).

### ZA-103 [Medium/High] Email confirmation always fails (code never decoded)

- Where: `ZauberCMS.Core/Membership/Services/MembershipService.cs:854`, `ZauberCMS.Core/Email/Services/EmailService.cs:46`, `ZauberCMS.Components/Account/Pages/ConfirmEmail.razor:47-50` · Category: C/D · Since: 2026-11-25
- Issue / Trigger: The token is Base64Url-encoded when sent but passed raw to `ConfirmEmailAsync`; `IsEmailUpdate` is ignored. `SignInRequireConfirmedAccount = true` is therefore unusable (keeps ZA-023 open).
- Fix: decode before confirming; handle the change-email branch. · Blocked by: nothing.

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

- Where: `ZauberCMS.Core/Extensions/StringExtensions.cs:315`, `Membership/ZauberSignInManager.cs:149` · Category: D

### ZA-032 [Low/High] Placeholder secrets in packaged appsettings

- Where: `ZauberCMS/appsettings.json:51,59,69` · Category: D
- Reconciled 2026-11-25: placeholders are now at lines 68-69 in all three appsettings files.

### ZA-033 [Low/Medium] Soft-deleted users keep username/email unique-index slots and PII

- Where: `MembershipService.cs:371-376` · Category: C/D · Issue: same email may be unable to re-register; GDPR erase path absent.
- Reconciled 2026-11-25: currently unreachable from the admin UI because user delete is broken (ZA-045); no restore or purge UI exists.

### ZA-085 [Low/High] SEO checker blocks the circuit: sync HTTP, no timeout or cancellation

- Where: `ZauberCMS.Components/Seo/SeoChecker.razor:53-56`, `ZauberCMS.Core/Seo/Checks/BrokenLinksCheck.cs:54-70` · Category: A · Since: 2026-11-25
- Issue / Fix: Same lines as ZA-019; async load with timeout and a disposed `CancellationTokenSource`.

### ZA-104 [Low/High] Image-resize cache is unbounded and upscaling is on

- Where: `ZauberCMS.Core/Settings/ZauberSettings.cs:48,60` · Category: D · Since: 2026-11-25
- Issue / Fix: Anonymous size/quality variations can fill the disk; default `AllowUpscale=false` and a finite `MaxCacheBytes`.

### ZA-105 [Low/High] Public `ExternalLoginAsync` skips the verified-email and `CanSignIn` gates

- Where: `ZauberCMS.Core/Membership/Services/MembershipService.cs:791-810` · Category: D · Since: 2026-11-25
- Issue / Fix: Unused by shipped pages; remove the fallback or mark `[Obsolete]`.

### ZA-106 [Low/Medium] Login reveals unconfirmed accounts and re-sends confirmation emails

- Where: `ZauberCMS.Core/Membership/Services/MembershipService.cs:626-640` · Category: D · Since: 2026-11-25
- Issue / Fix: Generic message; throttle resends.

### ZA-107 [Low/High] Email addresses written to logs

- Where: `ZauberCMS.Core/Membership/Services/MembershipService.cs:643,664,750,920,972`, `ZauberCMS.Core/Membership/ZauberSignInManager.cs:76` · Category: D · Since: 2026-11-25
- Issue / Fix: Log the user id instead.

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

- Where: `ZauberCMS.Core/Jobs/DailyJob.cs:21-45`, `ZauberCMS.Components/Seo/Startup/SitemapGeneratorService.cs:33,43,108`, `ZauberCMS.Core/Middleware/CultureMiddleware.cs:41` · Category: A · Since: 2026-11-25
- Issue / Fix: `BackgroundService` + `PeriodicTimer`; forward `RequestAborted`.

### ZA-112 [Low/High] SortableList drops callback tasks; `DotNetObjectReference` can leak

- Where: `ZauberCMS.Components/Shared/SortableList.razor:73-99` · Category: A · Since: 2026-11-25
- Issue / Fix: Return the task; try/finally on dispose.

### ZA-113 [Low/High] EF used directly in a context menu, scope held across a dialog

- Where: `ZauberCMS.Components/ContextMenus/ContentMenus/RestrictAccessContextMenu.cs:53-76` · Category: A/E · Since: 2026-11-25
- Issue / Fix: Move to a service method.

### ZA-114 [Low/High] Layouts set process-wide culture; sitemap formats with current culture and drops the home page

- Where: `ZauberCMS.Components/Admin/Layout/SectionLayout.razor:81-85`, `ZauberCMS.Components/Pages/BlankLayout.razor:29-33`, `ZauberCMS.Components/Seo/Startup/SitemapGeneratorService.cs:66-69,173-174` · Category: A/C · Since: 2026-11-25
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

- Where: `ZauberCMS/appsettings.json:74` · Category: F · Since: 2026-11-25
- Issue / Fix: Web and template have it; behaviour identical via code defaults. Add it and a key-parity test.

### ZA-119 [Low/High] Package metadata gaps and csproj cruft

- Where: `ZauberCMS/ZauberCMS.csproj:83`, `ZauberCMS.Components/ZauberCMS.Components.csproj:19,34-41` · Category: F · Since: 2026-11-25
- Issue / Fix: No `PackageReadmeFile`, icon, SourceLink or symbols; project-wide `NoWarn CS0618` hides obsolete usage; duplicated transitive references.

### ZA-120 [Low/High] Discovery and configuration fail silently or late

- Where: `ZauberCMS.Core/Plugins/ExtensionManager.cs:43,68,192,203`, `ZauberCMS.Core/ZauberSetup.cs:70-72` · Category: F · Since: 2026-11-25
- Issue / Fix: Bootstrap skips are not logged; type cache keyed by `T` alone; mistyped `Zauber:Plugins:*` throws `KeyNotFoundException`; no options validation.

### ZA-121 [Low/High] DI leftovers

- Where: `ZauberCMS.Core/ZauberSetup.cs:95-102,198,201,236` · Category: F · Since: 2026-11-25
- Issue / Fix: Unused scoped `HttpClient` and `IUserEmailStore`; `SignInManager` registered twice; unused `Actions` on `ContentNavGroup`.

### ZA-122 [Low/High] Design-time factory loads `appSettings.json` (wrong case)

- Where: `ZauberCMS.Core/Data/ZauberDesignTimeDbContextFactory.cs:13-14,34` · Category: F · Since: 2026-11-25
- Issue / Fix: `migrations.ps1` fails on Linux/macOS.

### ZA-123 [Low/High] Template and sample rough edges

- Where: `ZauberCMS.Template/template/ZauberCMSTemplate.Site/Properties/launchSettings.json:19` · Category: F · Since: 2026-11-25
- Issue / Fix: Launch profile named "Umbraco.Web.UI"; no template `.gitignore`; no starter `_Imports`, layout or view; sample points at a non-existent `/Home/Error`.

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
- Issue / Fix: No test project, `Directory.Build.props`, central package management, `.editorconfig`, `global.json` or `.gitattributes`; CI runs no tests or vulnerability scan. Known deviations in `Docs/Standards.md` all still present (≈76 scope-per-method sites, 11 sync paging methods, sync seeding).

### ZA-132 [Medium/High] Admin authorisation has no defence in depth

- Where: `ZauberCMS.Components/Admin/_Imports.razor:3` (the only folder-wide `[Authorize]`), `ZauberCMS/Components/Routes.razor:10`, `ZauberCMS.Core/ZauberSetup.cs` (no fallback policy), `ZauberCMS.Core/Seo/` redirect save path · Category: D · Since: 2026-11-25 (split out of ZA-044)
- Issue / Trigger: ZA-044 was possible because admin protection is one attribute per folder. Any future `/admin/*` page added outside `Components/Admin/` (in this repo or a consumer plugin) is public unless its author remembers the attribute; mutating services do no role check of their own; redirect targets may be external hosts.
- Fix: authorisation policy/convention covering every `/admin/**` route (fail closed), role checks in mutating services, validate redirect targets (local paths unless explicitly allowed). · Blocked by: design choice — affects consumer plugin pages (Roadmap Phase 4).

### ZA-134 [Medium/High] Rich-text editor 3.x always strips `iframe` (and other non-policy tags) from stored content

- Where: `ZauberCMS.Components/Editors/ZauberCMSEditorProperty.razor` (uses the default `HtmlPolicy`); server policy `ZauberCMS.Core/Shared/Services/DefaultHtmlSanitizerService.cs:18` allows `iframe` "for video embeds" · Category: C · Since: 2026-11-25 (RTE 2.1.0 → 3.0.0)
- Issue / Trigger: RTE 3.x sanitises the initial value in the browser. Tags outside `HtmlPolicy.AllowedTags` are removed on load and lost on the next save; `iframe`, `video`-style embeds via `object`/`embed`, `form` controls, `svg`, `style` and `script` are removed **whatever the policy says**. A site that pasted a YouTube iframe into a ZauberCMS RTE property in 4.x loses it the first time an editor opens and saves that content after upgrading. The sample database has no such content (checked); real sites may.
- Fix (owner decision): **Owner decision 2026-11-25:** embeds come with **ZauberCMS.RTE 4**, which ships with the .NET 11 move (Roadmap Phase 12): an Embed toolbar item storing a `figure` + `data-embed-*` placeholder, allow-listed providers, and conversion of existing allow-listed iframes on load instead of stripping. The CMS then needs a render step that expands the placeholder to an iframe on the public site. Until then, extend `HtmlPolicy.AllowedTags` for any other tags the CMS wants to keep (`video`, `audio`, `source`, `mark`, …), and put the behaviour in the upgrade notes for the release. · Blocked by: owner decision; ties to the Phase 4 sanitiser-policy question (ZA-021).

### ZA-135 [Medium/High] Opening a rich-text value rewrites content links to relative URLs

- Where: `ZauberCMS.Components/Editors/ZauberCMSEditorProperty.razor` (`ValidateEmbeddedReferences`, the `content.Url()` comparison); compare `Editors/Dialogs/ContentLinkDialog.razor:164-168`, which adds the leading `/` · Category: C · Since: 2026-11-25 (observed in the browser during the Phase 1 smoke test; the code predates the upgrade)
- Issue / Trigger: The link dialog stores `href="/blog"`. On the next open, the validator compares that with `content.Url()` (`blog`, no leading slash), decides the URL changed, rewrites it to `href="blog"` and raises `ValueChanged`. After the next save the link is relative to the current page, so on any page that is not at the root it points to the wrong URL.
- Fix: normalise both sides the same way (one shared helper for "site-relative URL of content"), and only rewrite when the normalised values differ. Add a test. · Blocked by: nothing (Phase 5).

### ZA-136 [Low/High] Workarounds for ZauberCMS.RTE 3.0.0 to remove when 3.1 ships

- Where: `ZauberCMS.Components/Editors/ToolbarItems/InsertContentLinkItem.cs`, `Editors/Dialogs/ContentLinkDialog.razor:141` · Category: F · Since: 2026-11-25
- Issue / Trigger: Found in the browser on 3.0.0. (1) `GetLinkAtCursorAsync` throws when the cursor is not in a link (empty interop stream) — the item now checks `api.State.InLink` first. (2) `SelectLinkAtCursorAsync` after `RestoreSelectionAsync` (dialog closed) does not select the link, so the edited link nested inside the old one — the item now selects the link *before* opening the dialog. (3) `LinkInfo.Rel` is missing in 3.0.0, so editing an existing link does not pre-fill Rel. (4) Editing an existing content link on the Manual tab drops `data-contentid` (not new in 3.x).
- Fix: when RTE 3.1 is published, bump both csprojs, restore `_rel = ExistingLink.Rel`, re-test (1) and (2) and drop the workarounds if fixed; carry `data-contentid` through the Manual tab. · Blocked by: RTE 3.1 release.

## Fixed this run / Resolved since last run

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
- Highest-value first tests (none exist): recycle-bin restore/final delete (ZA-001); soft-delete query defaults
  (ZA-016); external sign-in with and without a verified-email claim (ZA-002); user delete persists (ZA-045, ZA-088);
  DI container validation for all three providers (ZA-011); `BlockListState` units (ZA-034…ZA-037); draft save leaves
  blocks unpublished (ZA-038); no pending model changes ×3 and an empty database migrates (ZA-050); discovery
  snapshot by type name; template smoke test with appsettings key parity.
