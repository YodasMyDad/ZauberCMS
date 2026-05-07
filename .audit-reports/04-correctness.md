# Correctness & Bugs Audit

_Generated: 2026-05-07 14:55 UTC_
_Build status after fixes: PASSED_

## Summary
| | Found | Fixed | Skipped |
|--|--|--|--|
| Critical | 0 | 0 | 0 |
| High | 8 | 4 | 4 |
| Medium | 9 | 4 | 5 |
| Low | 6 | 0 | 6 |

## Findings

### [HIGH] Front-end serves recycle-binned content
- **File:** `ZauberCMS.Core/Content/Services/ContentService.cs:1989-2008` (`FetchEntryModelAsync`)
- **Confidence:** High
- **Status:** FIXED
- **Issue:** The front-end content lookup performed by `GetContentFromRequestAsync` filters by `Published` but never by `Deleted`. A content row that has been soft-deleted via `DeleteContentAsync(MoveToRecycleBin=true)` retains `Published == true` (the recycle-bin sets only `Deleted = true`), so any anonymous request for its slug, root content, or matched-domain content keeps rendering the recycled item until the cache (5 min) expires or a hard-delete happens.
- **Trigger:** Admin moves a published page to the recycle bin → public visitors continue receiving 200 OK with the deleted page's HTML for at least the cache TTL, and indefinitely if the same slug is hit on every refresh.
- **Why it matters:** Content the editor believed was offline is still publicly served. Violates the "soft-delete only" rule from CLAUDE.md (every list query must filter `Deleted == false` for live data).
- **Fix applied:** Added `&& !c.Deleted` to all three primary lookup queries (root content, domain match, slug match) and to the internal-redirect follow-up query.

### [HIGH] Recycle bin doesn't fire `OnContentDeleted` event
- **File:** `ZauberCMS.Core/Content/Services/ContentService.cs:360-366` (`DeleteContentAsync`, MoveToRecycleBin path)
- **Confidence:** High
- **Status:** FIXED
- **Issue:** The recycle-bin branch only sets `content.Deleted = true` and saves; the corresponding hard-delete branch a few lines below calls `appState.NotifyContentDeleted(...)` after success. As a result `OnContentDeleted`/`OnContentChanged` subscribers (admin tree refresh, listings) never get notified when content is recycle-binned — only when it is permanently deleted.
- **Trigger:** From the admin, soft-delete a page from the content tree → other open admin tabs / sections relying on `OnContentChanged` show the recycled item until manually refreshed.
- **Why it matters:** Stale UI state — subscribers don't refresh.
- **Fix applied:** After a successful save, raise `appState.NotifyContentDeleted(content, username)` in the recycle-bin branch matching the hard-delete branch's behaviour.

### [HIGH] User-supplied content URLs are saved verbatim but read lower-cased
- **File:** `ZauberCMS.Core/Content/Services/ContentService.cs:147-153` (`SaveContentAsync`)
- **Confidence:** High
- **Status:** FIXED
- **Issue:** When the caller passes a non-empty `Content.Url` (e.g. via the admin URL editor), `SaveContentAsync` stores it verbatim. The slug-generation path in `SlugHelper` lower-cases by default, but that path only runs when the URL is null/whitespace. Meanwhile `DefaultContentFinder.TryFindContent` lower-cases the request slug before passing it to `GetContentFromRequestAsync`, and the SQL comparison in `FetchEntryModelAsync` is case-sensitive on case-sensitive collations (default for PostgreSQL and case-sensitive SQL Server collations).
- **Trigger:** Save content with `Url = "MyPage"` → request `/mypage` → SQL `c.Url == "mypage"` does not match `"MyPage"` on case-sensitive collations → 404. SQLite/SQL Server with default case-insensitive collation accidentally hides the bug; PostgreSQL does not.
- **Why it matters:** Provider-dependent 404s, and inconsistent lookup semantics across providers.
- **Fix applied:** When `parameters.Content.Url` is non-empty on save, lowercase it via `ToLowerInvariant()` so the write side matches the read-side normalisation.

### [HIGH] `GetUsersOrderBy.DateUpdated` orders by `DateCreated`; `DateUpdatedDescending` and `DateCreated` cases missing
- **File:** `ZauberCMS.Core/Membership/Services/MembershipService.cs:1035-1040`
- **Confidence:** High
- **Status:** FIXED
- **Issue:** The user-query order-by switch had `GetUsersOrderBy.DateUpdated => query.OrderBy(p => p.DateCreated)` (wrong column) and was missing the `DateUpdatedDescending` and `DateCreated` enum cases entirely (both fell through to the default which orders by `DateCreated DESC`). The enum exposes all four values but only two were honoured.
- **Trigger:** Admin user grid is asked to sort by "Most recently updated" → silently sorted by oldest-created instead.
- **Why it matters:** Wrong ordering is one of the most user-visible correctness bugs.
- **Fix applied:** Added the missing `DateUpdatedDescending` and `DateCreated` switch arms and corrected `DateUpdated` to order by `p.DateUpdated`.

### [HIGH] `MembershipService.DeleteUserAsync` hard-deletes Identity user without cascading custom data
- **File:** `ZauberCMS.Core/Membership/Services/MembershipService.cs:357-388`
- **Confidence:** High
- **Status:** not fixed
- **Issue:** `DeleteUserAsync` hard-deletes via `userManager.DeleteAsync(user)`. The `User` entity tracks `PropertyData` (UserPropertyValues) and `Audits` collections; if those navigations don't have cascade-delete configured in the relevant provider, deletion either fails (FK violation) or leaves orphans. Also the User has a `Deleted` flag inherited from CMS conventions but it is never used here — unlike Content which is soft-deleted.
- **Trigger:** Delete a user that has property data → either FK exception or orphaned `ZauberUserPropertyValues` rows.
- **Why it matters:** Data integrity drift between providers; also a potential GDPR-style delete-cascade gap on associated audit/property data.
- **Why not fixed:** Confirming cascade-delete behaviour requires inspecting all three provider configurations and possibly migration changes; the audit prompt forbids fixes that require migrations. Also touches user-management workflow. Flag for human review.
- **Notes:** Compare to `DeleteContentAsync` which explicitly cascades through nested rows and property data inside an explicit transaction.

### [HIGH] Admin IP whitelist redirect target is hard-coded to `/api/auth/logout`
- **File:** `ZauberCMS.Components/Admin/Layout/SectionLayout.razor:67-74`
- **Confidence:** High
- **Status:** not fixed
- **Issue:** The IP-whitelist enforcement on layout init redirects to a hard-coded `"/api/auth/logout"`. The application defines this URL via `Urls.ApiLogout` constant — using the literal here drifts whenever the constant is renamed and would silently leave admins unable to be ejected.
- **Trigger:** Future refactor renames `Urls.ApiLogout` → SectionLayout still posts to a 404.
- **Why it matters:** Brittle. Low likelihood of actually firing today since the URL hasn't changed; flagged so future refactors don't miss it.
- **Why not fixed:** Cosmetic — leave for code-style cleanup so the audit doesn't churn admin code.

### [HIGH] Soft-deleted content visible in single-item lookups (`GetContentAsync`)
- **File:** `ZauberCMS.Core/Content/Services/ContentService.cs:1800-1851` (`BuildQuery(GetContentParameters, ...)`)
- **Confidence:** High
- **Status:** not fixed
- **Issue:** `BuildQuery(GetContentParameters)` filters by `Published` (when `IncludeUnpublished=false`) but never by `Deleted`. `MainLayout.razor` uses `GetContentAsync(...) { ContentTypeAlias = "Website", IncludeChildren = true, Cached = true }` to fetch the website root — if that root is recycle-binned (or any deleted child), it leaks into the front-end navigation. The `Url()` path is partly mitigated by the front-end fix above, but children loaded via `GetContentAsync` are not.
- **Trigger:** Admin recycle-bins a navigation-included child content → child still appears in nav.
- **Why it matters:** Stale data on front-end navigation.
- **Why not fixed:** Adding a default `Deleted=false` filter here would break recycle-bin restore (`RestoreContextMenu`, `FinalDeleteContentContextMenu` use `GetContentAsync` to access deleted items). Proper fix is to add an explicit `IncludeDeleted` flag on `GetContentParameters` and audit every caller — that touches a dozen+ files including admin UX. Flag for human review.

### [HIGH] `MediaService.DeleteMediaAsync` performs a hard delete, but `Media` model has a `Deleted` flag and no recycle-bin filter
- **File:** `ZauberCMS.Core/Media/Services/MediaService.cs:178-215`, `Media/Models/Media.cs:124`, `MediaService.cs:289-324` (BuildQuery doesn't filter `Deleted`)
- **Confidence:** High
- **Status:** not fixed
- **Issue:** `Media.Deleted` exists but is unused. `DeleteMediaAsync` removes the row directly (`dbContext.Medias.Remove`) — no recycle-bin path. Meanwhile `BuildQuery` for both `GetMediaAsync` and `QueryMediaAsync` does not filter `Deleted`. This means: (a) the convention from CLAUDE.md ("soft-delete only") is violated for media; (b) if anything ever sets `Deleted = true` on a media row (e.g. during a refactor or via direct SQL), it silently appears in queries.
- **Trigger:** Long-running design drift — different from a single-incident bug.
- **Why it matters:** Documents an inconsistency that future contributors are likely to step on.
- **Why not fixed:** Reconciling Media to soft-delete is a multi-file design change (recycle bin UI for media doesn't exist) — flag for human review. Removing the unused `Deleted` field needs a migration. Either direction needs human decision.

### [MEDIUM] `ContentVersioningService` cache-invalidation prefix never matches
- **File:** `ZauberCMS.Core/Content/Services/ContentVersioningService.cs:122` and `:178`
- **Confidence:** High
- **Status:** FIXED
- **Issue:** Both `CreateVersionAsync` and `PublishVersionAsync` end with `cacheService.ClearCachedItemsWithPrefix($"Content_{content.Id}")`. Cache keys are formatted as `{TypeName}-{hash}` (see `CacheExtensions.ToCacheKey` — hyphen, not underscore), so the prefix `Content_{guid}` never matches any cached entry.
- **Trigger:** Publish a version → admin/front-end might still serve a cached pre-publish projection until the entity-level `SaveChangesAndLog` invalidation in the underlying save catches it (this _is_ how it currently works; the dead prefix call is just noise but it documents an incorrect intent).
- **Why it matters:** Dead code that hides a real intent (per-item invalidation). If anyone tries to add per-content cache scoping later, they'll inherit the wrong format.
- **Fix applied:** Replaced with `cacheService.ClearCachedItemsWithPrefix(nameof(Models.Content))` to match the actual key format used elsewhere.

### [MEDIUM] `MembershipService.SaveRoleAsync` always logs "Update" because `role.Id == Guid.Empty` is checked after `CreateAsync`
- **File:** `ZauberCMS.Core/Membership/Services/MembershipService.cs:454-482`
- **Confidence:** High
- **Status:** FIXED
- **Issue:** The action label was decided post-create with `role.Id == Guid.Empty ? "Create" : "Update"`. By the time the log line runs, `roleManager.CreateAsync(role)` has already populated `Id` (`Guid.NewSequentialGuid()`-ish via Identity), so the comparison is always false → every role save logs as "Update" even on create.
- **Trigger:** Create a new role → audit log row says "Update".
- **Why it matters:** Misleading audit trail.
- **Fix applied:** Track `isUpdate` boolean during the create/update branch (matches the pattern used by `SaveContentTypeAsync`, `SaveDomainAsync`, etc.) and use it to label the log entry.

### [MEDIUM] `LanguageService.SaveLanguageAsync` silently creates a new language if a non-existent id is supplied
- **File:** `ZauberCMS.Core/Languages/Services/LanguageService.cs:74-102`
- **Confidence:** High
- **Status:** FIXED
- **Issue:** When `parameters.Id != null` but the language with that id doesn't exist in the DB, the code falls through to the create path with `language = new Language()` (no id assignment) — caller asked to update a specific id and instead got a new row. Additionally, the duplicate-ISO check on line 97 didn't exclude the language being updated, so saving the same culture twice (after the early-return on identical ISO) would be hard to reach but the check was also subtly wrong.
- **Trigger:** Programmatic `SaveLanguageAsync(new SaveLanguageParameters { Id = someStaleGuid, CultureInfo = ... })` after a deletion → unexpected create.
- **Why it matters:** Surprises callers; pollutes the language list.
- **Fix applied:** Return an error result when an explicit `Id` is supplied but the language is not found, and exclude `language.Id` from the duplicate-ISO check.

### [MEDIUM] `ContentEditor.razor.RefreshContent` NREs when the content id no longer exists
- **File:** `ZauberCMS.Components/Admin/ContentSection/ContentEditor.razor:391-417`
- **Confidence:** High
- **Status:** FIXED
- **Issue:** After loading `Content` via `GetContentAsync`, the next line dereferences `Content!.ContentTypeId`. If the content id was deleted (or was never valid), `GetContentAsync` returns null and the `Content!` deref throws NRE in the editor's render path.
- **Trigger:** Open an admin link to a content id that has been hard-deleted in another tab.
- **Why it matters:** Editor crash with stack trace bubbles instead of a friendly notification.
- **Fix applied:** Added an explicit null check that surfaces a `ShowErrorNotification("Unable to load this content item")` and returns early.

### [MEDIUM] `BuildQuery(QueryContentParameters)` only filters `Deleted` when `IsDeleted` is non-null — but the default is `false`
- **File:** `ZauberCMS.Core/Content/Services/ContentService.cs:1882-1885`, `Content/Parameters/QueryContentParameters.cs:19`
- **Confidence:** Medium
- **Status:** not fixed
- **Issue:** Re-read carefully: `QueryContentParameters.IsDeleted` defaults to `false`, and `BuildQuery` filters `query.Where(x => x.Deleted == request.IsDeleted)` only when `IsDeleted != null`. Since the default is `false` (non-null), the filter does fire by default — so this is correct as written. **But** any caller that explicitly sets `IsDeleted = null` (intentionally or via reflection/binding) bypasses the filter and gets both sets back. No live caller does this today, but the enum-style would be safer than nullable-bool semantics.
- **Trigger:** Any caller that sets `IsDeleted = null`.
- **Why it matters:** Forward-compatibility risk; code review trap.
- **Why not fixed:** Refactor to enum changes a public-ish surface (parameter class) and would touch every caller. Leave for human review.

### [MEDIUM] Search uses `string.ToLower()` instead of `StringComparison.OrdinalIgnoreCase`
- **File:** `ZauberCMS.Core/Content/Services/ContentService.cs:881`, `:1917`, `:2077-2081`; `ZauberCMS.Core/Membership/Services/MembershipService.cs:559-561`, `:1013-1015`; `ZauberCMS.Core/Tags/Services/TagService.cs` (search), `Languages/Services/LanguageService.cs` (similar)
- **Confidence:** Medium
- **Status:** not fixed
- **Issue:** Multiple search/match call sites do `x.Name.ToLower().Contains(searchTerm.ToLower())`. In LINQ-to-Entities this gets translated to `LOWER()` calls in SQL, which is provider-dependent and breaks under Turkish locales (`I` ↔ `ı`). The native EF translation `EF.Functions.Like` or culture-invariant collations would be more correct. For pure C# (`MatchDomainWithContent` at `:2077-2081`), `StringComparison.OrdinalIgnoreCase` would be the correct fix.
- **Trigger:** A user with Turkish (`tr-TR`) locale searches for content with `I` in the name → no matches.
- **Why it matters:** Locale-specific search bugs.
- **Why not fixed:** This is a multi-file change spanning every service's search code path. Flag for human review and a focused fix sprint.

### [MEDIUM] `TagService.SaveTagItemAsync` saves with `SaveChangesAsync` and skips `SaveChangesAndLog`
- **File:** `ZauberCMS.Core/Tags/Services/TagService.cs:222`
- **Confidence:** High
- **Status:** not fixed
- **Issue:** `SaveTagItemAsync` calls `dbContext.SaveChangesAsync` directly instead of `SaveChangesAndLog`. As a consequence: (a) the `Tag` cache prefix is not invalidated after tag-item changes, (b) `IBeforeEntitySave<TagItem>` / `IAfterEntitySave<TagItem>` plugins never fire for this path, and (c) zero-row-saved warning logging is bypassed.
- **Trigger:** Update tags on a content item → tag-listing caches keep stale `TagItem` projections until the cache TTL expires.
- **Why it matters:** Stale tag UI and missed plugin hooks.
- **Why not fixed:** The fix is mechanical (`return await dbContext.SaveChangesAndLog(...)`), but it changes a public-API success path and the `HandlerResult<TagItem>` return shape would need verification across callers. Flag for human review.

### [MEDIUM] `ContentService.GetContentLanguagesAsync` doesn't filter `Deleted`
- **File:** `ZauberCMS.Core/Content/Services/ContentService.cs:1331-1357`
- **Confidence:** High
- **Status:** not fixed
- **Issue:** The cached `{Url|Id} → LanguageIsoCode` lookup includes deleted content rows, so a soft-deleted content item still influences culture resolution for its slug. Specifically, `FetchEntryModelAsync` consults this dict to pick a culture for the request, and a deleted row's URL would return the wrong culture if the slug is reused.
- **Trigger:** Soft-delete a content item with URL `/about` and language ja-JP, create a new content with the same URL in en-US → culture-middleware may pick ja-JP.
- **Why it matters:** Wrong culture rendered.
- **Why not fixed:** Fix is one line (`.Where(x => !x.Deleted)`), but cache invalidation here ties into wider patterns — the dictionary is keyed by the Content type prefix and is invalidated on content saves. Out of pure caution, flag for review along with the broader Content/Deleted-filter clean-up.

### [MEDIUM] `GenerateUniqueUrl` checks all content (including soft-deleted) for slug collisions
- **File:** `ZauberCMS.Core/Content/Services/ContentService.cs:2098-2114`
- **Confidence:** High
- **Status:** not fixed
- **Issue:** When auto-generating a slug for a new content item, `GenerateUniqueUrl` collides with any row that holds that URL — including ones in the recycle bin. So if `/about` was recycled, creating new "About" content gets `/about-1` instead of being free to reuse `/about`.
- **Trigger:** Recycle a content row, then create a new one with the same name.
- **Why it matters:** Surprising URL behaviour; could be intentional (preserve URLs in case of restore) or accidental (no thought behind it).
- **Why not fixed:** The current behaviour is defensible (preserves URL on restore). Choosing the other behaviour is a UX decision. Flag.

### [MEDIUM] `StringExtensions.ToValue<T>` throws on bad property data for non-string `T`
- **File:** `ZauberCMS.Core/Extensions/StringExtensions.cs:60-127`
- **Confidence:** Medium
- **Status:** not fixed
- **Issue:** For `bool`, `int`, `decimal`, `Guid`, the method falls back to `JsonSerializer.Deserialize<T>(value)` if `TryParse` fails. `JsonSerializer.Deserialize<bool>("yes")` throws `JsonException`, which is _not_ caught by the wrapping `try` (the `try` is only on the final generic deserialize at the bottom). So a content property that contains `"yes"` and is read via `GetValue<bool>` propagates a `JsonException` up the render stack, blowing up the page.
- **Trigger:** Bad/legacy property data in DB read with the wrong `T` from a Razor view.
- **Why it matters:** Crashes on bad data instead of returning `default`.
- **Why not fixed:** The fix needs a small refactor to wrap each numeric/bool fallback in `try/catch` returning `default`. Low risk but touches a load-bearing extension; flag for review.

### [LOW] `_contentValues` cache throws on duplicate property aliases
- **File:** `ZauberCMS.Core/Content/Models/Content.cs:162-165`, `ZauberCMS.Core/Membership/Models/User.cs:26-29`
- **Confidence:** Medium
- **Status:** not fixed
- **Issue:** `PropertyData.ToDictionary(x => x.Alias, x => x.Value)` throws `ArgumentException` if two property values share an alias (which can happen if a content type's alias was renamed and old data lingers). The exception isn't caught, so first call to `GetValue<T>` on such content throws.
- **Why it matters:** Bad data crashes views.
- **Why not fixed:** A defensive `.GroupBy(...).ToDictionary(g => g.Key, g => g.First().Value)` fixes it but masks a data-integrity issue. Better to flag.

### [LOW] `ContentPickerProperty` returns single-Guid string for one item, JSON list for multiple
- **File:** `ZauberCMS.Components/Editors/ContentPickerProperty.razor:138-150`
- **Confidence:** High
- **Status:** not fixed
- **Issue:** When the picker has 1 item it stores the GUID as a plain string (`item.Id.ToString()`); 2+ items get JSON-serialised. Consumers (`GetContentItems` in `ContentExtensions`) handle both shapes by sniffing for `[`, but other consumers reading via `GetValue<List<Guid>>("alias")` will get null/empty when only one item is picked.
- **Why it matters:** Surprising consumer ergonomics. Existing code paths handle it; new code can easily get it wrong.
- **Why not fixed:** Behaviour change; existing data on disk uses both shapes. Flag.

### [LOW] `MembershipService.LoginUserAsync` reveals account existence
- **File:** `ZauberCMS.Core/Membership/Services/MembershipService.cs:651-655`
- **Confidence:** High
- **Status:** not fixed
- **Issue:** `if (user == null) loginResult.AddMessage("You are do not have an account, please register", ResultMessageType.Error)` — discloses that the email is unregistered, an enumeration channel for harvesting known/unknown emails. (Also typo: "You are do not".) The forgot-password path elsewhere correctly uses a constant-time response.
- **Why it matters:** Account enumeration. Auth-related, security-adjacent.
- **Why not fixed:** Auth-flow change requiring product-level call. Note in the security audit too.

### [LOW] `MembershipService.UpdateSecurityStampAsync` only runs when `refreshCurrentUser == false`
- **File:** `ZauberCMS.Core/Membership/Services/MembershipService.cs:191-194`, `:333-337`
- **Confidence:** Medium
- **Status:** not fixed
- **Issue:** The condition is documented as "only refresh stamp if we're not about to refresh-sign-in," which is correct because `RefreshSignInAsync` updates the stamp implicitly. But if the caller doesn't honour `RefreshSignIn` (e.g. ignores the result flag), an admin-issued role change for the current user leaves the original cookie working — until the 10-minute revalidation. Whether `RefreshSignIn` is honoured is the caller's responsibility.
- **Why it matters:** Possible privilege-change delay.
- **Why not fixed:** Behaviour-by-design; flag for visibility.

### [LOW] `BuildQuery(QueryContentParameters)` AsNoTracking applied AFTER `Where` filters
- **File:** `ZauberCMS.Core/Content/Services/ContentService.cs:1910-1913`
- **Confidence:** Low
- **Status:** not fixed
- **Issue:** `AsNoTracking()` applied late in the chain still applies to the materialised result, but conventionally it's set first to make tracking semantics obvious. Functionally identical here; cosmetic.
- **Why not fixed:** Cosmetic.

### [LOW] `MembershipService.LoginUserAsync` and `RegisterUserAsync` swallow exceptions but don't log them
- **File:** `ZauberCMS.Core/Membership/Services/MembershipService.cs:657-661`, `:742-746`, `:816-820`
- **Confidence:** Medium
- **Status:** not fixed
- **Issue:** Catch blocks set the result message to `e.Message` and return — but no log entry is emitted. Surfacing exception messages to the UI is also a small information leak.
- **Why not fixed:** Trade-off (error messages help users); flag.

## Files modified
- `ZauberCMS.Core/Content/Services/ContentService.cs` — front-end query filters `Deleted`, recycle-bin notifies content-deleted, user-supplied URLs are lowercased on save.
- `ZauberCMS.Core/Content/Services/ContentVersioningService.cs` — fixed cache-invalidation prefix from `Content_{id}` (underscore, never matches) to `nameof(Content)` (matches `{TypeName}-{hash}` keys).
- `ZauberCMS.Core/Membership/Services/MembershipService.cs` — fixed `GetUsersOrderBy` switch (DateUpdated cases were ordering by DateCreated and missing arms); fixed role create/update detection so audit log labels match reality.
- `ZauberCMS.Core/Languages/Services/LanguageService.cs` — `SaveLanguageAsync` returns "not found" when caller passes a non-existent id (instead of silently creating); duplicate-ISO check now excludes the language being updated.
- `ZauberCMS.Components/Admin/ContentSection/ContentEditor.razor` — added null-guard around `Content` after `GetContentAsync` to avoid NRE on missing/deleted ids.

## Areas you did not fully cover
- **Admin Razor pages beyond `ContentEditor.razor`** — full pass would need to read every `*.razor` for null derefs and missing notifications. Spot-checked the most touched ones.
- **Content versioning service end-to-end** — read for the cache-prefix bug and high-level flow; didn't audit the version-restore JSON-diff machinery.
- **Property editors (~30 files)** — verified the `Settings.FromJson<T>()` round-trip is used by the ones that have settings; didn't deep-dive each editor for individual edge cases.
- **`SaveChangesAndLog`-based plugin lifecycle** — `IBeforeEntitySave<T>` / `IAfterEntitySave<T>` semantics looked sane; not exhaustively tested.
- **Pagination boundary conditions** — `ToPaginatedList` was not opened; flagged callers that pass `Skip(...)` and `Take(...)` directly look correct but a deep audit of the paginator class itself wasn't done.
