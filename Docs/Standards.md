# Engineering Standards

The bar for every change. **New and touched code meets the target; untouched legacy code is fixed in its own scoped
change, not drive-by.** Where the codebase currently differs, the "Known deviations" list says so — don't copy those
patterns into new code.

## 1. Compatibility first (this is a NuGet product)

- Every public type in Core/Components/Routing/ZauberCMS is consumed by sites we can't see. Semantic versioning:
  breaking a public signature, parameter class, discovery interface, config key or persisted format = **major**.
- Deprecate before removing: `[Obsolete("Use X. Removed in vN.")]`, keep the old path working for one major.
- Persisted identifiers are data: `Type.FullName` of views/editors, property aliases, `GlobalData` aliases, JSON value
  shapes of editors, cache-prefix names. Changing any needs a migration/compat path.
- New behaviour is opt-in or backwards compatible by default (settings default to current behaviour).
- New config goes under `Zauber:*`, bound to `ZauberSettings` (or a sub-options class), with a safe default added to
  **all three** `appsettings.json` files (`ZauberCMS/`, `ZauberCMS.Web/`, template site).

## 2. C# and project conventions

- Nullable reference types on; no `!` suppression without a one-line reason. No `#pragma warning disable` without a
  justification comment and the narrowest scope.
- File-scoped namespaces, primary constructors for DI, collection expressions (`[]`), `var` when the type is obvious,
  pattern matching over casts. PascalCase for constants. One public type per file; models in `<Feature>/Models/`.
- Mapping is hand-written (`XMappingExtensions.MapTo`) — no AutoMapper. **Adding a property means updating `MapTo`**
  or updates are silently dropped.
- Strings: `StringComparison.Ordinal`/`OrdinalIgnoreCase` explicitly; never `ToLower()` for comparisons. In queries,
  use `EF.Functions.Like`/collation-aware comparisons that work on all three providers.
- Time: `DateTime.UtcNow` everywhere persisted; PostgreSQL rejects non-UTC `DateTimeKind`.
- No new dependency without a reason in the PR; prefer the BCL and what's already referenced. No MediatR.

## 3. Async, lifetimes, DI

- Async all the way. No `.Result`, `.Wait()`, `GetAwaiter().GetResult()` (one documented exception in `ZauberSetup`).
  No sync EF calls (`ToList`, `FirstOrDefault`, `Count`) inside async methods — use the `…Async` overloads.
- Every public async method takes `CancellationToken cancellationToken = default` **last** and forwards it to EF,
  HTTP and I/O. Components pass a token tied to their lifetime where the work can outlive the component.
- `async void` only for event handlers, and then wrapped in `try/catch` with logging (an escaped exception kills the
  Blazor circuit).
- No fire-and-forget on request/circuit paths; background work belongs in a hosted service / `Channel`.
- Lifetimes: services Scoped (per request / per circuit), `AppState` Singleton, `TreeState` Scoped. Never capture a
  scoped service in a singleton. `HttpClient` only via `IHttpClientFactory`.
- DbContext: obtained per operation (current pattern: `using var scope = serviceScopeFactory.CreateScope()` →
  `IZauberDbContext`). Never inject a DbContext into a component; never share one across `Task.WhenAll`.

## 4. Data access (EF Core, three providers)

- Reads: `AsNoTracking()` (applied before filters), project to what you need, paginate (`Skip/Take` with a stable
  `OrderBy`), `Any()` not `Count() > 0`, no N+1 — batch with `Contains` or `Include`. Split queries are the default.
- Soft delete filter on Content/Media/User **in every query** (no global filters exist).
- Writes: `SaveChangesAndLog`; multi-step writes in `BeginTransactionAsync`; respect the `Content.DateUpdated`
  concurrency token.
- Raw SQL only when unavoidable, parameterised, using `Zauber*` table names, and valid on SQL Server, SQLite **and**
  PostgreSQL (quote identifiers). Dynamic LINQ strings from the browser must pass an allow-list.
- Schema: see [Data.md](Data.md). Three migrations, always via `migrations.ps1`.

## 5. Blazor

- **Render modes**: front-end views are static SSR. Interactivity is opted into per child component
  (`@rendermode InteractiveServer`). No Radzen, Blazored.Modal or admin JS on the front-end.
- SSR forms: `[SupplyParameterFromForm]`, `<AntiforgeryToken />`/`EditForm` with `FormName`, enhanced navigation aware.
- Components that prerender run `OnInitializedAsync` twice — make it idempotent and cheap, or persist state
  (`[PersistentState]` in .NET 10).
- Subscriptions (`AppState`, `TreeState`, `NavigationManager.LocationChanged`) are made in `OnInitialized` and
  removed in `Dispose`/`DisposeAsync`. Event callbacks from other threads → `InvokeAsync(StateHasChanged)`.
- JS interop only in `OnAfterRenderAsync(firstRender)`; dispose `IJSObjectReference` / `DotNetObjectReference`;
  catch `JSDisconnectedException` on dispose.
- Lists: `@key` on repeated components; `Virtualize` for large sets. Don't call `StateHasChanged` in loops.
- Don't put logic in markup; keep components thin and push work into services. No EF in components.
- Admin UI: Radzen components + the shared building blocks (`EditorRow`, `PropertyInfo`, `TreeButton`,
  `OpenSidePanel`, notification extensions). See [AdminUI.md](AdminUI.md). Accessible labels on inputs; keyboard usable.

## 6. Errors, logging, results

- Service writes return `HandlerResult<T>` with `ResultMessage`s; reads return `T?` / `PaginatedList<T>`.
  Don't throw for expected outcomes (validation, not found, concurrency); do throw for programmer errors.
- Never show `ex.Message` to end users; log the exception, return a generic message.
- `ILogger<T>` with message templates (`logger.LogWarning("Content {ContentId} missing", id)`) — no string
  interpolation in log calls, no PII/secrets in logs. Use `[LoggerMessage]` source-gen on hot paths. Don't add new
  static `Serilog.Log.*` calls.
- Don't swallow exceptions: every `catch` logs or rethrows.

## 7. Security baseline

- Admin pages/components: `[Authorize(Roles = Constants.Roles.AdminRoleName)]` (automatic only inside
  `ZauberCMS.Components/Admin/`). Controllers/endpoints authorise explicitly; state-changing endpoints are POST +
  antiforgery.
- Output: never render user/content HTML via `MarkupString` without `IHtmlSanitizerService`.
- Uploads: extension allow-list + content sniffing + `MaxUploadSize`; never trust client paths; SVG is not safe by default.
- Redirects: local URLs only (`returnUrl` validated). Absolute links in emails must not be built from the `Host` header.
- Secrets never in committed `appsettings*.json`; use user-secrets / environment / vault.
- Outbound HTTP to user-supplied URLs needs an allow-list and timeouts (SSRF).

## 8. Performance

- Front-end hot path: `CatchAll` → `EntryPage` (reuses `CultureMiddleware.GetEntryModel`, no second lookup) → view.
  Every query on that path is cached (`Cached = true`) or justified.
- Don't call `ExtensionManager.GetInstances<T>()` per item in a loop; resolve once.
- `[GeneratedRegex]` for regexes; avoid allocations in loops; stream large files.

## 9. Testing

Run: `dotnet test --solution ZauberCMS.sln -c Release` (repo root). CI runs it on every PR and before publishing.
`global.json` selects the Microsoft.Testing.Platform runner, which xUnit v3 needs on the .NET 10 SDK.

- `tests/ZauberCMS.Core.Tests` — service tests. `Infrastructure/TestSite` builds the **real** DI graph with
  `AddZauberCms`, a private in-memory SQLite database created by the real migrations, the seeders, and a signed-in
  user (`TestSite.SignIn`). The web server is not started. Create a `TestSite` per test class (`IAsyncLifetime`),
  call services through a scope, and assert on what is stored with `QueryDatabaseAsync` (fresh DbContext).
- `tests/ZauberCMS.Components.Tests` — plain unit tests of component-side models (`BlockListState`) and the
  snapshot of shipped property-editor type names (`Discovery/Snapshots/PropertyEditors.txt`: adding an editor means
  adding a line; a line may never be removed or renamed — the names are stored in site databases).
- Settings shared by both projects live in `tests/Directory.Build.props` (warnings are errors there).
- **Every bug fix has a regression test.** A test that describes the correct behaviour of a bug that is still open
  is committed with `Skip = "ZA-NNN (open): …"` after it has been seen to fail; the fix removes the skip. Never
  write a test that asserts the buggy behaviour.
- Test the behaviour through the public service or type, not private details. One behaviour per test, named as a
  sentence. No shared mutable state between tests; no sleeps; no network.
- Not built yet: bUnit component tests, tests on SQL Server/PostgreSQL/Redis (Phase 6), and a template smoke test
  (`dotnet new zaubercms` → build). UI behaviour without a test still needs its manual verification written down.

## 10. Repo hygiene (target)

Not in place yet; add in their own change, not mixed with features: `Directory.Build.props` (shared TFM, nullable,
`AnalysisLevel=latest`, `TreatWarningsAsErrors` once the warning baseline is zero), `Directory.Packages.props` (central
package management — keeps the 4 packages' dependency versions aligned), `.editorconfig`, `global.json` (pin SDK),
CI running tests and `dotnet list package --vulnerable`.

## Known deviations (exist today — do not copy)

| Pattern in code | Target | Example |
| --- | --- | --- |
| `IServiceScopeFactory` scope per method + `GetRequiredService` inside services | `IDbContextFactory<T>` / constructor injection (planned refactor) | `Content/Services/ContentService.cs` |
| `AddDbContext` + separate `AddScoped<IZauberDbContext>` → two contexts per scope (Identity has its own tracker) | single registration | `ZauberSetup.cs` provider switch |
| `PaginatedList` ctor does sync `Count()`/`ToList()` wrapped in `Task.FromResult` | async paging | `Shared/Models/PaginatedList.cs` |
| `SaveChangesAndLog` swallows exceptions into UI message text | log + generic message | `Extensions/DbContextExtensions.cs` |
| Static `Serilog.Log.*` alongside `ILogger<T>` | `ILogger<T>` only | `DbContextExtensions.cs`, `ZauberSetup.cs` |
| `ToLower()` search comparisons | provider-safe comparisons | `ContentService`, `MembershipService` |
| `ISeedData.Initialise` is synchronous | async seeding | `Data/Interfaces/ISeedData.cs` |
| Some service methods without `CancellationToken` | token on every async API | `IContentService.Export/ImportContentTypeAsync` |
| `PendingModelChangesWarning` suppressed | detect drift in CI | the three DbContexts |
