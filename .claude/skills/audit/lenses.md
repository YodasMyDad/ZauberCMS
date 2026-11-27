# Audit lenses

Every agent reads **Rules for every agent**. Code agents (units, sweeps, the diff reviewer) then apply lenses A–F;
product agents apply G and H from [product.md](product.md). Each item below is a question to answer by reading code,
not a pattern to grep. Grep finds the places; reading decides whether something is wrong.

## Rules for every agent

### Output files

Two files per agent. `<run>/<agent-id>.tsv` has one line per item, tab-separated, for the merge agent:

```text
kind  key                                   category  severity|value  confidence|effort  open-id  title
F     ZauberCMS.Core/Jobs/DailyJob.cs#ExecuteAsync  A   High   High   -        An exception in the job stops the site
P     Components/Admin/ContentSection#publish  H      M      S      -        Publish with descendants
S     -                                      -         -      -       ZA-111   still open
```

(Columns are aligned here for reading; write real tabs.) `kind` is F (finding), P (proposal) or S (status of an open item). `key` is `path#member` (a folder or screen for a
proposal). Write the `.tsv` line when you write the item, not at the end. `<run>/<agent-id>.md` holds the detail:

```markdown
# <agent-id>: <unit or lens name>
## Findings
### [Severity/Confidence] Title
- Where: `path:line` (+ other places with the same root cause) · Category: A–H · Open ID: <ZA-NNN if it matches one>
- Issue: 1–3 sentences: what is wrong.
- Trigger: what someone does, or what state the site is in, for it to happen, and at what size it hurts (pages,
  media, editors, requests per second, circuit lifetime). Required for Critical and High.
- Evidence: the decisive lines (≤ 5, quoted) or what you observed in the browser or the log.
- Fix: the smallest change that removes the cause. · Fix risk: public API? schema? persisted format? UX? callers?
- Test: the regression test that would have caught it (class and behaviour, as a sentence).
## Proposals
### [Value H/M/L · Effort S/M/L] Title
- Section: … · Lens: G | H | F (extensibility)
- Today: what the user does now (screen, steps) or what a site builder must do.
- Proposal: the change, kept as small as covers the common need.
- Why: the benefit to editors or site builders; for parity, what Umbraco does (docs.umbraco.com link).
- Needs: owner decision | design | nothing · Risk to existing sites: what a site that updates would notice.
## Status of open items
- ZA-NNN: still open (new `path:line`) | fixed (commit) | changed (how)
## Coverage
- Read fully: <files> · Skimmed: <files, why> · Not read: <files, why> · Not checked: <items, why>
```

### Severity (calibrate with these, not with gut feel)

| Severity | Meaning in this CMS | Examples |
| --- | --- | --- |
| Critical | Anyone (an anonymous visitor, or an editor doing normal work) can cause site-wide data loss or corruption, get admin access, run code, or take the site down | anonymous request reaches an admin service; a save deletes other pages' blocks; a request pattern that exhausts memory on every node |
| High | Data loss or corruption for an item, privilege escalation, stored XSS, the circuit or the process dies on a common path, visitors shown wrong or unpublished content, a common editor workflow unusable, unbounded memory growth under normal traffic | publish drops a draft; a leak per page view; a cache that serves one language's page in another |
| Medium | Wrong under a specific but plausible condition; slow or memory-hungry at realistic size (1k pages, 10k media, 20 editors, 50 req/s); a bounded leak per circuit; unsaved work lost on a less common path; a missing protection that another control mitigates | N+1 in a tree of 500 nodes; a stale cache after a move on a second server; Cancel in a nested panel keeps changes |
| Low | Edge cases, hardening, maintainability with a concrete cost, small UX defects | a misleading message; a missing `CancellationToken` on a quick query |

Confidence: **High** = you read the whole path (or reproduced it); **Medium** = you read the main path, one link not
traced; **Low** = a suspicion. Low-confidence findings are verified or dropped before the report.

### Finding or proposal?

- An editor can **lose work, be misled, get stuck, or be shown something wrong** → a finding (Category G).
- It **works, but could be easier, faster, or closer to Umbraco** → a proposal.
- A site builder **cannot extend or replace** something they reasonably need to → a finding (F) if it breaks the
  pluggable/updatable principle in `AGENTS.md`, otherwise a proposal.

### Precision

- Cite only lines you read. Name the trigger in user terms. Mark "needs repro" when it depends on runtime behaviour
  you did not observe.
- One root cause = one finding, listing every place. Don't split one bug across lenses.
- Before proposing a feature, grep for it under other names and look in the admin. Many "missing" features exist.
- No style or naming-taste items, and nothing that is only "consider X" without a cost.
- Don't re-report an open ID; say its status instead. If yours is the same root cause, give its ID.

### Not findings (exempt)

- The two documented sync-over-async start-up calls in `ZauberSetup` (database initialise, culture query); the
  middleware order; dual Project/PackageReference; the `WeakEventManager` design; missing `ConfigureAwait(false)`;
  the tracked `ZauberCMS.Web/app.db`; code style preferences.
- Anything in `Docs/Standards.md` → Known deviations (one roll-up line, not one per occurrence).
- Anything an owner decision (D-number in `Docs/Roadmap.md`) settled, unless the code differs from the decision.
- A proposal the owner declined (product.md, "Different on purpose", and history.md "declined").

## A. Async, concurrency, lifetimes, memory

### A1 Async correctness

- Sync-over-async anywhere but the two exempt calls. Sync EF (`ToList`, `First…`, `Count`, `Any`, `SaveChanges`) or
  sync I/O (`File.ReadAll*`, `Stream.Read`, `ReadToEnd`) inside async code or on a request or circuit path.
- `async void` outside event handlers, and the hidden kind: async lambdas passed as `Action` (`List.ForEach(async …)`,
  timer callbacks, `Parallel.ForEach(async …)`, an `Action` parameter or event), and `Select(async …)` whose tasks are
  never awaited.
- Fire-and-forget on request or circuit paths (`_ = XAsync()`, unawaited calls, `Task.Run` nobody observes): the
  exception vanishes or ends the process, and the work outlives its scope and DbContext.
- Hosted services: since .NET 6 an exception that escapes `BackgroundService.ExecuteAsync` **stops the whole host**
  (`BackgroundServiceExceptionBehavior.StopHost`), so one passing database error in `DailyJob` or
  `SitemapGeneratorService` takes the site down. Each loop iteration catches, logs and carries on; `StartAsync` of an
  `IHostedService` (the Redis bus start-up) doesn't block or throw for a dependency that is briefly down.
- `CancellationToken`: public async APIs take it last and pass it to EF, HTTP and I/O; components cancel work that
  can outlive them (loads on navigation, search-as-you-type) on dispose; hosted services and loops honour
  `stoppingToken`; `Task.Delay` gets the token.
- Every outbound call (HTTP, SMTP, Redis, storage, the SEO checker) has a timeout.
- `ValueTask` awaited twice or stored; `TaskCompletionSource` without `RunContinuationsAsynchronously`;
  `.ConfigureAwait(false)` followed by touching component state.

### A2 Concurrency and shared state

- A DbContext, `UserManager`, `SignInManager` or other scoped service used by two operations at once:
  `Task.WhenAll`, parallel loops, or a component that starts a second load (a second event, a parameter change)
  before the first one finishes.
- Check-then-act on shared state (`ContainsKey` then `Add`, `TryGetValue` then write) on non-concurrent collections
  in singletons or statics. `ConcurrentDictionary.GetOrAdd` with a factory that does I/O or has side effects (it can
  run twice). A cached `Task` or `Lazy<Task>` that faulted, so the failure sticks.
- `SemaphoreSlim.Wait()` in async code; `Release` outside `finally`; a semaphore per key that is never removed.
- Singleton or static state written per request (lists, dictionaries, options objects changed after load).
- Process-wide state changed per request: `CultureInfo.CurrentCulture`/`DefaultThreadCurrentCulture` (ZA-114), environment.
- Cache stampede: many concurrent misses for one hot key after a clear all go to the database.
- Ordering across servers: a cache clear sent before the transaction commits lets another server re-cache old data;
  the window between commit and the Redis clear (`ICacheInvalidationBus`) is the stale window.
- Blazor re-entrancy: a second click while a save runs (two saves, duplicate rows); state that changed during an
  `await` used afterwards; an older response overwriting a newer one (no cancellation or sequence check).
- `AppState` events arrive on the publisher's thread: handlers use `InvokeAsync`, and do nothing once their component
  is disposed.

### A3 Lifetimes and DI

- A scoped service captured by a singleton, including indirectly: an `IServiceProvider` kept by a singleton and
  resolved later, or extension instances built with `ActivatorUtilities` from one scope and cached for the app's
  life (check how `ExtensionManager` creates and caches instances).
- `IDisposable` transients resolved from the root provider (kept until shutdown).
- `CreateScope()` without `using`; a scope disposed while something it returned is still used (an `IQueryable`, a
  lazy-loaded entity, a service).
- `IHttpContextAccessor`/`HttpContext` inside an interactive circuit: the admin is not prerendered, so it is null;
  elsewhere it is stale. Callers go through `ICurrentUserAccessor`.
- `IOptions<T>` where the value can change at runtime (installer, admin settings) and `IOptionsMonitor<T>` is needed.
- `IStartupPlugin` / `IExternalAuthenticationProvider` need a parameterless ctor (AGENTS.md rule 3).

### A4 Memory and resources

- Unbounded growth: static or singleton collections keyed by URL, user, query, culture, file or regex; `ServerCache`
  or `IMemoryCache` entries without a size limit or expiry; strong references from singleton events to components
  (events other than `AppState`'s weak ones).
- Per-circuit memory: what a circuit holds while an editor keeps the tab open (whole trees, full `Content` graphs with
  every property value, media lists, base64 images, uploaded bytes, `ContentEditScope` snapshots), multiplied by
  editors × tabs; disconnected-circuit retention (`CircuitOptions`).
- Large allocations (> 85 KB, the large object heap) per request or render: whole files read into `byte[]` or
  `MemoryStream`, a content graph serialised to a string to copy or compare it, `ToList()` of a large query to count
  or page it in memory. Prefer streams and projections.
- Uploads and images: `OpenReadStream(maxAllowedSize)` limits; files staged to disk, not memory (`StagedBrowserFile`);
  temp files removed on every path (success, failure, cancel, circuit end); image objects disposed; pixel limits.
- Not disposed: `CancellationTokenSource` (above all with `CancelAfter`), timers, `IJSObjectReference`,
  `DotNetObjectReference`, `HttpResponseMessage`, streams, scopes, connections, Redis subscriptions; a component with
  a `Dispose` method but no `@implements IDisposable`.
- Subscriptions (`AppState`, `TreeState`, `LocationChanged`, `EditContext.OnFieldChanged`, JS callbacks) without a
  matching unsubscribe; subscribing in `OnParametersSet` (one more handler per render); subscribing a lambda.
- EF change tracker growing in loops (imports, bulk copies, sorts): `AsNoTracking`, or `ChangeTracker.Clear()` per batch.
- **Shared mutable cache entries**: a cached entity graph handed to many circuits and requests, then changed by one
  caller (the change leaks to every reader and into the next save). Check what callers do with what the cache returns.

### A5 More than one server

D1 makes PostgreSQL and Redis first-class, and a site that uses Redis runs on several servers. Anything that lives in
one process is then wrong on the others:

- `AppState` events reach only the server that raised them: the Redis bus carries cache clears, not events, so
  another server's admin trees, lists and open editors stay stale. Is that visible to an editor, and is it said?
- Per-process state: rate-limit partitions (each server allows the full limit), `InstallState`, in-memory locks and
  semaphores, static caches outside `ICacheService`.
- Per-server disk: media on `DiskStorageProvider`, `_mediacache`, sitemap files, temp uploads. Which need shared
  storage, and do the docs say so?
- Hosted services run on every server: `DailyJob` pruning twice at once, sitemaps written twice. Umbraco runs
  once-only jobs through a database-tracked distributed job; what stops two of ours overlapping?
- Data-protection keys (Standards §7), sticky sessions for circuits, and forwarded headers per node.

## B. Data access and performance

### B1 Queries

- N+1: queries in loops (per block, picker item, tree node, menu item, user, tag); `GetInstances<T>()` per item.
- Materialise-then-filter (`ToList()` before `Where`, `Skip`, `Count`); `Count() > 0` instead of `Any()`; `Include`
  of unused collections; several collection `Include`s without split queries (cartesian explosion); whole entities
  loaded to read one column.
- `AsNoTracking()` on reads, applied before the filters. A tracked read that is then cached costs memory and
  carries stale state.
- Unbounded lists: every admin list, picker, tree level and export pages; a page size from the browser is capped.
- Indexes: each frequent `Where`/`OrderBy`/join on Content, ContentVersion, UnpublishedContent, Media, Audit,
  redirects, tags and users has an index in the model snapshots; a unique index wherever code assumes uniqueness
  (aliases, URLs).
- Search: leading-wildcard `Contains` or `ToLower()` on big tables; provider differences (PostgreSQL is case-sensitive,
  SQLite `LIKE` is case-insensitive only for ASCII); `EF.Functions.Like`.
- `Contains` over large id lists (parameter limits); chunk them.
- Writes: `SaveChanges` per row in a loop; a transaction held across slow work (HTTP, file I/O, image processing),
  which blocks other writers (SQLite has one writer: "database is locked" with several editors).
- Provider portability: `DateTime.Now` and non-UTC `DateTimeKind` (PostgreSQL rejects them), `HasColumnType`, raw SQL
  quoting, sequential-GUID ordering per provider, decimal precision, string length limits.

### B2 Caching

- Every front-end hot-path query is cached or justified (Standards §8). The key holds every dimension that changes
  the result: culture, published vs draft, the viewer's roles for restricted content, the page number. The number
  of keys is bounded.
- Every write path clears every prefix whose data it changes (a content save → URLs, routes, navigation, sitemap; a
  content type → content; media → pickers and the restricted-media lookup; settings → whatever read them), on every
  server (the Redis bus), after the commit.
- Cached 404s are bounded and cleared when a page is created at that URL.

### B3 Front-end request path (measure, don't guess)

- Round-trips for a page cold, warm, 404, with a block list, with pickers, restricted: compare with the last report's
  budget (SKILL.md §2).
- Middleware cost on every request, including static files and media: does each lookup (culture, redirects,
  restricted media, admin access) return early for requests it does not concern?
- HTTP caching: `Cache-Control`/ETag on media, resized images and fingerprinted assets; compression; range
  requests for media; image-resize limits (ZA-104).
- Work done in layouts on every request (menus, settings, dictionary) is cached.

### B4 Admin rendering

- A parent `StateHasChanged` or an `EventCallback` that re-renders every child (trees, block lists, the content
  editor's properties); heavy components without `ShouldRender`; lists that reorder without `@key` (wrong state is
  reused as well as slow).
- Work in markup or property getters run on every render (LINQ over big lists, JSON parsing, `GetInstances`);
  `OnParametersSetAsync` querying on every parent render.
- Large lists without `Virtualize` or paging: media folders, users, audit log, list view, content-type dropdowns.
- SignalR payload: large render diffs or parameters (rich-text values, base64 images), JS interop calls in loops,
  `MaximumReceiveMessageSize` against paste and upload sizes.
- Start-up: reflection scanning, seeders doing work on every boot, time to the first request.

### B5 Algorithms and allocation

- O(n²) over pages or media (nested loops, `List.Contains` or `FirstOrDefault` inside loops; use a dictionary or set).
- A `Regex` built per call or without `[GeneratedRegex]`; a regex from user data without a timeout (also D).
- String building in loops; LINQ allocations in per-request middleware; interpolated log messages (use templates,
  `[LoggerMessage]` on hot paths).

## C. Correctness: ZauberCMS invariants

- **Soft delete** (rule 4): every Content/Media/User query filters `Deleted` in the right direction; recycle-bin paths
  pass `IncludeDeleted`; callers of any `Get*`/`Query*` whose filter default changed; recycle, restore and purge act
  on the whole subtree; recycled media is not served.
- **Writes** (rule 5): the whole change staged, `SaveChangesAndLog` called once, never `SaveChangesAsync` before it;
  related cache prefixes cleared; `MapTo` copies every mutable property.
- **Save hooks** (D28, D29): run for every row written; one veto stops the whole change; a draft runs the
  `UnpublishedContent` hooks. After-save hooks run before the commit (ZA-163, open).
- **Transactions and partial failure**: multi-step writes in one transaction; database and file work ordered so a
  failure leaves no orphan in either direction (file without row, row without file).
- **Concurrency**: the `Content.DateUpdated` token is honoured on every content write path (editor, sort, move, copy,
  restore, import), and the second editor gets a clear message.
- **Tree integrity**: path, level and sort order after create, move, copy, sort, restore; moving into one's own
  descendant refused; a copied subtree's internal references (blocks, pickers) handled deliberately.
- **Publish state**: published, draft and versions consistent after publish, unpublish, save draft, restore version,
  copy, move, recycle, restore; blocks follow their page ([Docs/BlockListEditor.md](../../../Docs/BlockListEditor.md)).
- **Nested content**: every block found through `INestedContentReferenceProvider`; delete, copy and versions cover
  the whole nest; never a block saved on its own from the editor.
- **Server-side validation** for every write, not only in the admin form: required properties, alias uniqueness
  (D31), URL uniqueness, reserved paths (`/admin`, `/_content`, `/api`, `/Account`; ZA-095).
- **Stored formats** (D30, D31): written invariant, read in the old and new shapes; editor settings round-trip
  (`GetPropertySettings`/`SavePropertySettings`/`FromJson<T>`).
- **Persisted type names**: views, editors, settings fallbacks and hard-coded type-name strings still resolve.
- `CatchAll` lives in `ZauberCMS.Routing`, and Routing is scanned last.
- `AppState` events raised exactly once per change, with the post-change entity.
- **Languages**: content-language resolution, domains, a missing translation's fallback, dictionary fallback.
- **Time**: stored UTC, shown in one consistent zone; date-only values not shifted by the time zone.
- **Results**: `HandlerResult.Success` correct on every path (no success with errors, no failure without a message),
  and the message reaches the user.
- Null safety (`GetValue<T>`, `FirstOrDefault` unchecked), duplicate keys in `ToDictionary`, entities in sets or keys
  compared by reference, `StringComparison` missing, off-by-one paging (1-based `PageIndex`), empty, whitespace,
  maximum and over-length values (a string longer than its column fails in the database, not in validation).
- Swallowed exceptions: every `catch` logs or rethrows; `finally` restores busy flags and state.

## D. Security

- **Access**: every page or component under `/admin` outside `Components/Admin/` carries its own `[Authorize]`
  (`AdminAccessMiddleware` and `Routes.razor` are the safety net); controllers and endpoints (`AuthController`, any
  minimal API) authorise explicitly. Services do not authorise (D27), so the calling page is the only gate: look for
  admin services reachable from a front-end interactive component or an endpoint. IDOR on content, media, users, roles.
- **Circuits**: an admin circuit that outlives a role removal, a lockout or a sign-out (auth-state revalidation
  interval, security stamp).
- Admin IP allow-list bypass; rate limits on every login-like, registration and reset page; lockout on every sign-in
  path (password, external, refresh).
- **Identity**: the installer and the installed flag (reachable or reopened after install, on every node and by a
  direct POST?), `AdminEmailAddresses` promotion (only when the owner proved the address), `RecoverAdminUser`,
  external login (`state`, verified-email mapping, auto-link by email; ZA-105), security-stamp rotation on privilege
  change, cookie flags, account enumeration and timing on login/register/forgot/resend (ZA-106), token lifetimes and
  single use (ZA-150).
- **Injection**: `FromSqlRaw`/`ExecuteSqlRaw` with concatenation; Dynamic LINQ from the browser without
  `GridQueryGuard`; `Type.GetType` or `Activator` on a name from a form, query or JSON (stored type names must resolve
  only to discovered types); polymorphic JSON with type names.
- **XSS**: `MarkupString`/`Html.Raw` without `IHtmlSanitizerService`; the sanitiser policy (D7: `iframe` only from the
  allow-list, `data:` only for images); JS `innerHTML`; rich-text embeds through `IRichTextEmbedService`.
- **CSRF**: state-changing GETs (ZA-030); antiforgery on SSR forms; mass assignment (form or JSON models that bind
  roles, ids, owners or flags).
- **Uploads**: extension allow-list + `UploadContentCheck` + `GlobalSettings.MaxUploadFileSizeInBytes` on every path (admin, editors, any
  `IStorageProvider`); SVG through `SvgSanitizer` (D6); path traversal in names and folders; zip slip in any import;
  `RestrictedMediaMiddleware` bypass (case, encoding, the resizer, `_mediacache`); image-resize dimension DoS.
- **Outbound and redirects**: SSRF (the SEO checker and any URL fetch use `SeoHttp`), open redirects (`returnUrl`,
  `RedirectTarget.IsValid`; D11 allows other sites on purpose), Host-header links in email (`ToAbsoluteUrl`),
  `AllowedHosts`.
- **ReDoS**: user-defined redirect patterns and any regex over request data run with a timeout.
- **XML**: sitemap or import parsers with DTD processing on (XXE).
- **Headers**: CSP on the admin and media, `X-Content-Type-Options`, `frame-ancestors`/`X-Frame-Options` (admin
  clickjacking), `Referrer-Policy`, HSTS in production.
- **DoS**: page sizes from the query string, expensive endpoints without a rate limit (image resize, SEO checker,
  sitemap generation), large form posts, SignalR message size.
- **Secrets and privacy**: secrets in config, logs or UI; PII in logs (ZA-107); weak crypto (MD5/SHA1 for security,
  `Random` for tokens).
- **Production**: detailed errors off, HSTS/HTTPS, forwarded headers, data-protection keys persisted on multi-node
  sites. **Supply chain**: vendored code (`Components/Modal`, `wwwroot/lib`) at a known-vulnerable version; CI workflow
  permissions, pinned actions, secret scope.

## E. Blazor and render modes

- **Boundaries**: no Radzen, Blazored.Modal, admin JS or `@rendermode` leaking into front-end views; interactive
  opt-ins are per component, with serialisable parameters, and they still work prerendered (runs twice) and as plain SSR.
- **SSR forms** (front end, account pages, installer): `FormName`, `[SupplyParameterFromForm]`, antiforgery,
  enhanced form posts, post-redirect-get, validation messages rendered in SSR, double submits.
- **Enhanced navigation** on the front end: scripts that must run on every page; the admin always loads as a full page.
- **Parameters**: a component writing to its own `[Parameter]` (overwritten on the next parent render); a child
  mutating a parent-owned object; two-way binding loops; `@bind:after` vs `ValueChanged`.
- **Lifecycle**: loading in `OnParametersSetAsync` without checking the route value changed (AdminUI.md: it drops
  unsaved edits); `OnAfterRenderAsync` calling `StateHasChanged` every time; JS interop before the first render;
  navigation in `OnInitialized` during prerender.
- **Closures**: a `for` loop variable captured in `@onclick` lambdas (every item gets the last index).
- **Errors** (D32): code we do not own (editors, dashboards, plugin components, dialogs) sits inside
  `AdminErrorBoundary`; boundaries reset on navigation; an exception in an event handler outside a boundary ends the
  circuit. Raw exception text is never shown to users; notifications use the extension helpers.
- **Unsaved changes**: editors and side panels guard navigation (`NavigationLock`, close and cancel paths); what
  happens to an open panel and its edits after a reconnect or a server restart.
- `CascadingValue` without `IsFixed` for values that never change; large objects cascaded.
- **JS**: interop only in `OnAfterRenderAsync(firstRender)`; modules and `DotNetObjectReference` disposed;
  `JSDisconnectedException` caught on dispose; unhandled promise rejections; no inline script where a CSP applies.
- Every `IContentView` has `@layout`; components do not call EF.
- **Accessibility defects**: inputs without labels, icon buttons without names, clickable `div`s, keyboard access to
  trees, menus and drag-and-drop, focus trapped in and returned from panels and dialogs, contrast. New instances go
  into the accessibility roll-up (ZA-128).

## F. Architecture, public API, extensibility, packaging, tests, docs, operability

- **F1 Public API and persisted contracts**: a break to a public type, discovery interface or parameter class without
  the `[Obsolete]` path or, during the rework, without a release-notes line (D18); persisted identifiers changed
  (type full names, aliases, `GlobalData` aliases, JSON value shapes, cache prefixes); the property-editor snapshot
  (`tests/ZauberCMS.Components.Tests/Discovery/Snapshots/PropertyEditors.txt`) kept current. **Upgrades**: a site on
  the last release must update without losing data. Check new migrations for drop-and-add renames, NOT NULL columns
  without a default, data steps that run per row, and SQLite table rebuilds on big tables. K's package validation
  lists the API breaks, and R1's upgrade probe starts this code on the last release's database.
- **F2 Pluggable and updatable** (the core principle in `AGENTS.md`). For each area: can a site add to it, customise
  it, or replace it from its own project (an interface, a DI registration, a setting) and then update the packages
  without redoing that? Look for hard-coded lists of built-in types, "our" types special-cased over a consumer's,
  extension types that are not public and non-abstract, registrations that a site's own registration cannot replace
  (`TryAdd` where replacement should work), new settings without a safe default in all three `appsettings.json`
  files, behaviour changes a consumer would notice without a release-notes line, locked-down paths without docs for
  plugin authors.
- **F3 Design**: logic in components that belongs in services; services doing UI or authorisation (D27); one rule
  implemented twice and drifting (URL normalisation, alias checks, culture resolution); dead or unwired code (ZA-090);
  very large classes (ZA-125); Known deviations copied into new code.
- **F4 Packaging and CI**: package contents and metadata; the template; `ZauberCMS.targets`; workflow permissions,
  pinned actions, publish order (ZA-089, ZA-109).
- **F5 Tests**: untested critical behaviour in the area (name each missing test as a sentence); tests that cannot
  fail (no assertion, assert the buggy behaviour, catch everything); a provider-dependent behaviour without its
  `…OnPostgreSql`/`…OnSqlServer` variant; sleeps, network, shared mutable state.
- **F6 Docs**: behaviour in `Docs/*.md` or `AGENTS.md` that the code no longer does; an extension point without
  docs; ReadMe and template instructions a new site builder would trip over.
- **F7 Operability**: logs that let a site owner diagnose a problem (which item, which user, which setting), at the
  right level (no Error for expected outcomes, no Information on every request); misconfiguration that fails at
  start-up with a clear message (ZA-120); error pages (ZA-098); whether the database, Redis, storage and SMTP can be
  checked from outside (health checks; compare with Umbraco's in product.md).
