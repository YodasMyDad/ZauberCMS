# ZauberCMS Audit Report

Audited commit: 3eb5cb7 · Date: 2026-11-25 · Scope: seed (not a full run) · Mode: report
Build: not run · Warnings: baseline not taken · Vulnerable packages: not checked · Outdated majors: not checked

> **Seed report.** Consolidates the still-open items from the retired `.audit-reports/` (02-memory, 04-correctness,
> 05-security — see git history at `332e7e4`) plus issues found while fact-checking the docs on 2026-11-25.
> Each item was re-checked against `3eb5cb7` by reading code, **not** by running the app. The async (A) and
> performance (B) categories have never been audited. Run `/audit full` to produce the first complete report.

## Summary

| Severity | Open | Fixed this run | Resolved since last run |
| --- | --- | --- | --- |
| High | 12 | 0 | see list below |
| Medium | 22 | 0 | see list below |
| Low | 9 | 0 | see list below |

## Open findings

### ZA-001 [High/High] Recycle-bin Restore and Final Delete silently do nothing (regression)

- Where: `ZauberCMS.Components/ContextMenus/RecycleBinMenus/RestoreContextMenu.cs:37`, `FinalDeleteContentContextMenu.cs:35` · Category: C · Since: `ca91ca1`
- Issue / Trigger: `GetContentAsync` now excludes deleted rows by default; these menus don't pass `IncludeDeleted = true`, get `null`, and return silently. Delete content → Recycle bin → Restore.
- Fix: pass `IncludeDeleted = true`; grep all other recycle-bin callers. · Blocked by: nothing (verify in app).

### ZA-002 [High/Medium] External (OAuth) sign-up likely always refused (regression)

- Where: `ZauberCMS.Core/Membership/ZauberSignInManager.cs:72-86` · Category: D · Since: `ca91ca1`
- Issue / Trigger: requires an `email_verified` claim, but no provider maps it (`ClaimActions.MapJsonKey`); stock Google/Facebook/Microsoft handlers don't emit it.
- Fix: map the claim per provider (Google has `email_verified`; others need a policy). · Blocked by: identity design decision; verify with a real provider.

### ZA-003 [High/High] Host-header injection in password-reset/confirm email links

- Where: `ZauberCMS.Core/Extensions/HttpContextExtensions.cs:22`; callers `MembershipService.cs:910`, `Email/Services/EmailService.cs:48`; `AllowedHosts: "*"` in all three appsettings · Category: D
- Fix: configured canonical site URL setting; restrict `AllowedHosts`. · Blocked by: new config key (all three appsettings).

### ZA-004 [High/High] First registered user becomes Admin (re-opens if all users soft-deleted)

- Where: `ZauberCMS.Core/Extensions/RoleExtensions.cs:31` · Category: D
- Fix: install-time setup flow / explicit bootstrap token. · Blocked by: product decision.

### ZA-005 [High/Medium] SVG uploads served same-origin unsanitised when opted in

- Where: `ZauberCMS.Core/Settings/GlobalSettings.cs:11-15`, `Providers/DiskStorageProvider.cs:82` · Category: D
- Fix: sanitise SVG or serve with `Content-Disposition`/CSP sandbox. · Blocked by: product decision.

### ZA-006 [High/Medium] Media recycle bin has no UI — soft-deleted media can't be restored or purged

- Where: `ZauberCMS.Core/Media/Services/MediaService.cs:200-214`, `ZauberCMS.Components/ContextMenus/MediaMenus/DeleteMediaContextMenu.cs:47` · Category: C
- Fix: media recycle-bin section or hard-delete with confirmation. · Blocked by: UX decision.

### ZA-007 [High/Medium] Raw SQL with unquoted identifiers likely fails on PostgreSQL

- Where: `ZauberCMS.Core/Extensions/DbContextExtensions.cs:25-53` (`WherePathLike`, `WhereHasCompositionsUsing`); used by copy (`ContentService.cs:819`) and restrict-access · Category: B/C
- Fix: quote identifiers per provider or replace with LINQ. · Blocked by: needs PostgreSQL verification.

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

### ZA-013 [Medium/High] Media delete notifies before save

- Where: `ZauberCMS.Core/Media/Services/MediaService.cs:214-216` · Category: C

### ZA-014 [Medium/High] Cache key registry unbounded/per-process under Redis

- Where: `ZauberCMS.Core/Shared/Services/DefaultCacheService.cs:15,52` · Category: A/B
- Issue: grows forever; prefix invalidation not multi-node safe. · Blocked by: Redis SCAN/tagging design.

### ZA-015 [Medium/High] `AddFluentValidation` never unsubscribes

- Where: `ZauberCMS.Core/Shared/Validation/EditContextFluentValidationExtensions.cs:22,25` · Category: A

### ZA-016 [Medium/Medium] Nullable `IsDeleted = null` bypasses soft-delete filter

- Where: `Content/Parameters/QueryContentParameters.cs:19`, `ContentService.cs:2045` · Category: C · Blocked by: public parameter class.

### ZA-017 [Medium/High] `ToLower()` search comparisons (Turkish-I, provider-dependent)

- Where: `ContentService.cs:1023,2080,2240-2244`; `MembershipService.cs:564-565,1029-1030` · Category: B/C

### ZA-018 [Medium/Medium] `SaveTagItemAsync` bypasses `SaveChangesAndLog` hooks

- Where: `ZauberCMS.Core/Tags/Services/TagService.cs:222-224` · Category: C

### ZA-019 [Medium/High] Admin SSRF via SEO checker

- Where: `ZauberCMS.Components/Seo/SeoChecker.razor:53-56`, `Seo/Checks/ResponseStatusCheck.cs:23`, `BrokenLinksCheck.cs:61` · Category: D · Blocked by: allow-list policy.

### ZA-020 [Medium/High] Dynamic LINQ from browser strings without allow-list

- Where: `ZauberCMS.Core/Data/Services/DataService.cs:137,143`, `Languages/Services/LanguageService.cs:402,408` · Category: D

### ZA-021 [Medium/Medium] Sanitiser allows `iframe` and `data:`; RTE rendered as raw `MarkupString`

- Where: `ZauberCMS.Core/Shared/Services/DefaultHtmlSanitizerService.cs:18,26`, `ZauberCMS.Web/ContentBlocks/RTEBlock.razor:6` · Category: D · Blocked by: product decision.

### ZA-022 [Medium/Medium] Upload check extension-only for text types

- Where: `ZauberCMS.Core/Providers/DiskStorageProvider.cs:55-63,85+` · Category: D

### ZA-023 [Medium/Medium] Promotion via `AdminEmailAddresses` without confirmed email

- Where: `RoleExtensions.cs:32-33`; `SignInRequireConfirmedAccount: false` · Category: D · Blocked by: deliberate feature.

### ZA-024 [Medium/Low] Registration flooding — only the shared "login" limiter, no CAPTCHA

- Where: `ZauberCMS.Components/Account/Pages/Register.razor:11` · Category: D · Blocked by: new dependency.

### ZA-025 [Low/High] Duplicate tree alias constant

- Where: `ZauberCMS.Core/Constants.cs:89` (`StructureElementTypeTree == "StructureContentTypeTree"`) · Category: C

### ZA-026 [Low/High] `<RadzenComponents/>` rendered twice

- Where: `ZauberCMS.Components/Admin/Layout/SectionLayout.razor:10,16` · Category: E

### ZA-027 [Low/Medium] Numeric editor stores values in current culture

- Where: `ZauberCMS.Components/Editors/NumericProperty.razor:41` · Category: C

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

### ZA-033 [Low/Medium] Soft-deleted users keep username/email unique-index slots and PII

- Where: `MembershipService.cs:371-376` · Category: C/D · Issue: same email may be unable to re-register; GDPR erase path absent.

### ZA-034 [High/High] Block list: Cancel in a nested modal does not discard its changes (data loss)

- Where: `ZauberCMS.Components/Admin/ContentSection/ContentEditor.razor:595-601`, `ZauberCMS.Components/Editors/Models/BlockListState.cs:358-367` · Category: C/E · Since: 2026-11-25 deep-dive
- Issue / Trigger: F1. Changes merge into ancestors immediately; Cancel clears only its own copy. Open B1, delete nested B2, Cancel, save → B2 hard-deleted while B1 still references it; cancelled adds become orphan rows.
- Fix: see [Docs/BlockListEditor.md](../Docs/BlockListEditor.md) §6 (EditSession + SaveContentTreeAsync) or §6.5 stopgaps.

### ZA-035 [High/High] Block list: root editor replaces pending changes instead of merging

- Where: `ZauberCMS.Components/Admin/ContentSection/ContentEditor.razor:613-621`, `ZauberCMS.Components/Editors/Models/BlockListState.cs:366` · Category: C/E · Since: 2026-11-25 deep-dive
- Issue / Trigger: F2. Two block-list properties on one type (or via composition) → only the last-touched property's changes are saved; duplicate `ChangesPending` call leaves root with only the nested subset.
- Fix: see [Docs/BlockListEditor.md](../Docs/BlockListEditor.md) §6 (EditSession + SaveContentTreeAsync) or §6.5 stopgaps.

### ZA-036 [High/High] Block list: nested update dropped when item already in Added

- Where: `ZauberCMS.Components/Editors/Models/BlockListState.cs:196-197` · Category: C/E · Since: 2026-11-25 deep-dive
- Issue / Trigger: F3. Add B1 with new B2, close, reopen B1, edit B2, save → B2 persisted with original values.
- Fix: see [Docs/BlockListEditor.md](../Docs/BlockListEditor.md) §6 (EditSession + SaveContentTreeAsync) or §6.5 stopgaps.

### ZA-037 [Medium/High] Block list: deleting a block keeps its pending descendants

- Where: `ZauberCMS.Components/Editors/Models/BlockListState.cs:107-142` · Category: C/E · Since: 2026-11-25 deep-dive
- Issue / Trigger: F4. New descendants of a deleted block are inserted as orphans; DB cascade reads stored JSON so never reaches them.
- Fix: see [Docs/BlockListEditor.md](../Docs/BlockListEditor.md) §6 (EditSession + SaveContentTreeAsync) or §6.5 stopgaps.

### ZA-038 [High/High] Block list: 'Save only' (draft) on published content saves blocks live

- Where: `ZauberCMS.Components/Admin/ContentSection/ContentEditor.razor:623-679`, `ZauberCMS.Core/Content/Services/ContentService.cs:214-270` · Category: C/E · Since: 2026-11-25 deep-dive
- Issue / Trigger: F6. Parent goes to UnpublishedContent but blocks are saved/deleted live; 'Clear changes' can't revert them.
- Fix: see [Docs/BlockListEditor.md](../Docs/BlockListEditor.md) §6 (EditSession + SaveContentTreeAsync) or §6.5 stopgaps.

### ZA-039 [Medium/Medium] Block list: editor state stale after save (needs repro)

- Where: `ZauberCMS.Components/Admin/ContentSection/ContentEditor.razor:681-780` · Category: C/E · Since: 2026-11-25 deep-dive
- Issue / Trigger: F7. Change lists and `DateUpdated` kept after save → second save gives false concurrency errors / failed deletes / re-adds.
- Fix: see [Docs/BlockListEditor.md](../Docs/BlockListEditor.md) §6 (EditSession + SaveContentTreeAsync) or §6.5 stopgaps.

### ZA-040 [High/Medium] Hard delete notifies `NotifyContentDeleted(null)` to every session

- Where: `ZauberCMS.Core/Content/Services/ContentService.cs:578`, `ZauberCMS.Components/Admin/ContentSection/ContentEditor.razor:607` · Category: C/E · Since: 2026-11-25 deep-dive
- Issue / Trigger: F9. Every open content editor in every circuit refreshes (unsaved edits lost), on the publisher's thread.
- Fix: see [Docs/BlockListEditor.md](../Docs/BlockListEditor.md) §6 (EditSession + SaveContentTreeAsync) or §6.5 stopgaps.

### ZA-041 [Medium/High] Block list: change bubbling is re-entrant and doubles per level

- Where: `ZauberCMS.Components/Editors/Models/BlockListState.cs:358-401` · Category: C/E · Since: 2026-11-25 deep-dive
- Issue / Trigger: F10. Change at depth d merged 2^(d-1) times through async void handlers.
- Fix: see [Docs/BlockListEditor.md](../Docs/BlockListEditor.md) §6 (EditSession + SaveContentTreeAsync) or §6.5 stopgaps.

### ZA-042 [Medium/Low] Block list: `OnParametersSetAsync` rebuilds WorkingCopy each time (needs repro)

- Where: `ZauberCMS.Components/Admin/ContentSection/ContentEditor.razor:364-367` · Category: C/E · Since: 2026-11-25 deep-dive
- Issue / Trigger: F12. If stacked modals re-supply parameters, in-modal edits reset and child callbacks point at disposed components.
- Fix: see [Docs/BlockListEditor.md](../Docs/BlockListEditor.md) §6 (EditSession + SaveContentTreeAsync) or §6.5 stopgaps.

### ZA-043 [Medium/Medium] Block previews: Blazor-owned nodes moved into shadow root

- Where: `ZauberCMS.Components/ContentComponents/RenderBlock.razor:12-18,50`, `ZauberCMS.Components/wwwroot/js/shadow-dom.js:40-42` · Category: C/E · Since: 2026-11-25 deep-dive
- Issue / Trigger: F13. New nodes may render outside the shadow root (invisible), clicks inside previews don't fire, no isolation without stylesheets. Fix direction: static HtmlRenderer previews.
- Fix: see [Docs/BlockListEditor.md](../Docs/BlockListEditor.md) §6 (EditSession + SaveContentTreeAsync) or §6.5 stopgaps.

## Fixed this run / Resolved since last run

- (legacy) DeleteUserAsync hard delete → soft delete (`ca91ca1`)
- (legacy) Hard-coded logout URL → `Urls.ApiLogout`
- (legacy) `GetContentAsync` missing Deleted filter → filtered (caused ZA-001)
- (legacy) `GetContentLanguagesAsync` included deleted; `GenerateUniqueUrl` collided with deleted rows
- (legacy) `ToValue<T>` threw on bad data; login revealed account existence; swallowed exceptions now logged
- (legacy) OAuth trusted unverified email (caused ZA-002); `ShowDetailedErrors` limited to non-Production
- (legacy) SortableList JS module disposal

## Deterministic checks

Not run — first `/audit full` will establish the build-warning and package baselines.

## Coverage

Seed only. Categories A (async) and B (performance) have never been reviewed; E and F only via the docs fact-check.
