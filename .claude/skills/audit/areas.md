# Audit areas: units, sweeps, product groups

A `full` run gives **every unit to its own agent**, which reads every file in it end to end and applies lenses A–F.
Then the **sweeps** go across the whole codebase, one lens each, by building an inventory (every singleton, every
cache key, every endpoint…) and checking each entry. The **product groups** look at the product as editors and site
builders use it (lenses G and H, [product.md](product.md)). A file that belongs to no unit goes to the nearest unit
by folder, or to U16; say so in coverage.

Sizes were measured on 2026-11-27, in lines of `.cs`, `.razor`, `.js` and `.css`, leaving out `bin`, `obj`,
`Migrations`, `wwwroot/lib` and `node_modules`. Re-measure, then split a unit above ~5k lines by subfolder and merge
one below ~1k into a neighbour:

```powershell
Get-ChildItem <folders> -Recurse -File -Include *.cs,*.razor,*.js,*.css | Where-Object FullName -notmatch '\\(bin|obj|Migrations|lib|node_modules)\\' | ForEach-Object { [IO.File]::ReadAllLines($_.FullName).Length } | Measure-Object -Sum
```

For `changed`, map each changed file to the unit whose paths contain it, then group the touched units into at most
four agents. Each agent reads the changed files in full, plus their direct callers and callees.

## Units

Paths are relative to the repo root. `Core` = `ZauberCMS.Core`, `Components` = `ZauberCMS.Components`.

### U1 Bootstrap, configuration and plumbing (~3.2k)

- **Paths**: `Core/ZauberSetup.cs`, `Core/Constants.cs`, `Core/Urls.cs`, `Core/Plugins/`, `Core/Settings/`, `Core/Data/`
  (contexts, `DataService`, design-time factories; of `Migrations/`, only parity and the newest migration of each
  provider), `Core/SeedData/`, `Core/Jobs/`, `Core/Middleware/`, `ZauberCMS/` (`Components/App.razor`,
  `Components/Routes.razor`, `ZauberCMS.targets`, `appsettings.json`), `ZauberCMS.Routing/`.
- **Focus**: which assemblies are scanned, and in what order (Routing last); discovery visibility (public,
  non-abstract); new middleware placed with a reason (the order is exempt, a new entry isn't); the DbContext chosen
  per scope and the start without a database; seeders idempotent and cheap, since they run on every boot; migration
  safety for a site upgrading from the last release (lenses F1); `DailyJob` (an exception that escapes it stops the
  host, lenses A1; overlapping itself; two servers running it at once, A5; cancellation; batch sizes; D25); the per-request cost of the
  culture, redirect and error-page middleware; config defaults and start-up validation (ZA-120); render-mode choice in
  `App.razor`; the admin gate in `Routes.razor`.
- **Risk**: AGENTS.md "Bootstrap". **Docs**: AGENTS.md, Services.md.

### U2 Shared services: cache, sanitiser, embeds, validation, audit, email (~2.5k)

- **Paths**: `Core/Shared/`, `Core/Audit/`, `Core/Email/`.
- **Focus**: cache keys (every dimension present, cardinality bounded), clearing by prefix (which writes clear which
  prefixes), size limits and expiry in `ServerCache`, stampede, cached objects handed out shared and mutable;
  `RedisCacheInvalidationBus` (reconnect, lost messages, clear before or after commit; D13); sanitiser policy against
  D7 and the embed allow-list; `LayoutResolverService`; `ValidateService` and `IValidate<T>`; save-hook contracts;
  audit writes and retention.
- **Docs**: Services.md "Cache".

### U3 Extensions and helpers (~3.7k)

- **Paths**: `Core/Extensions/`.
- **Focus**: these run everywhere, so a slow or allocating helper here sits on the hot path.
  `DbContextExtensions.SaveChangesAndLog` (rule 5, D28: hooks for every row, one veto, the transaction, cache
  clearing, exception handling, a Known deviation); value conversion (`ValueConversionExtensions`,
  `PickerValueExtensions`, `PropertyExtensions`: invariant culture and both stored shapes, D30/D31); `SlugHelper` and
  `StringExtensions` consistent with `ContentUrl.Normalize`; `HtmlSanitizationExtensions`, `HttpContextExtensions`,
  `IdentityExtensions`, `NavigationManagerExtensions`, `MediaExtensions`, `CacheExtensions`.

### U4 Content services (~4.8k)

- **Paths**: `Core/Content/Services/`, `Core/Content/ContentFinders/`, `Core/Content/Validation/`, and the files at
  the root of `Core/Content/` (`ContentUrl.cs`: URL normalisation, which every slug and link must go through).
- **Focus**: `SaveContentAsync` and `NestedContentTree` (blocks saved, deleted, versioned and copied with the page);
  publish, draft and version consistency; recycle, restore and purge of whole subtrees; copy, move and sort (tree
  integrity, moving under one's own descendant); the `DateUpdated` concurrency token; cache clearing per write;
  `ContentService.Access` (restricted content); front-end queries (cached?); tree and list paging (D26); the
  versioning service and retention (D25).
- **Risk**: AGENTS.md "Block List editor & nested content", "Soft delete". **Docs**: BlockListEditor.md, Services.md "Deletes".

### U5 Content model and tags (~2.7k)

- **Paths**: `Core/Content/{Models,Mapping,Parameters,Interfaces}/`, `Core/Tags/`.
- **Focus**: parameter defaults (soft-delete filters: flipping one silently breaks callers, as ZA-001 did); `MapTo`
  copies every mutable property; EF mappings (indexes, lengths, the concurrency token, cascades); the public surface
  planned for v5 (ZA-092) and `IContentProperty`'s Radzen types (ZA-091); tag links follow live values (D29).
- **Docs**: Data.md.

### U6 Media and storage (~2.9k)

- **Paths**: `Core/Media/`, `Core/Providers/` (`DiskStorageProvider`, `SmtpEmailProvider`, `ProviderService`).
- **Focus**: upload checks on every path; database and file work ordered so a failure leaves no orphan; the recycle
  bin (D3) and purge removing files; `RestrictedMediaMiddleware` bypasses (case, encoding, the resizer, `_mediacache`;
  ZA-155); `MediaSecurityHeadersMiddleware`; paths resolved under the web root; SMTP (timeouts, TLS, credentials kept
  out of logs); provider selection through `Zauber:Plugins:*`; what a custom `IStorageProvider` must do (Standards §7).

### U7 Membership, identity and account pages (~4.2k)

- **Paths**: `Core/Membership/`, `Core/Security/`, `Components/Account/`, `Components/Controllers/`.
- **Focus**: every sign-in path and its lockout and rate limit; external login (ZA-105); registration on/off and form
  protection (D23, ZA-149, ZA-150); admin promotion (D22, as built); `RecoverAdminUser`; roles, claims and security-stamp
  rotation; `ICurrentUserAccessor` and `RunAs` (D27); `AdminAccessMiddleware` and the path-segment match (ZA-095);
  user delete (ZA-033); account-page SSR forms; `IAccountLayout`; enumeration (ZA-106); PII in logs (ZA-107).
- **Docs**: Installer.md (first administrator, registration), Services.md "Callers and access".

### U8 Installer (~3.0k)

- **Paths**: `Core/Installer/`.
- **Focus**: it runs with no database, no static files and nobody signed in, and it is open to the first visitor
  (D20) until it finishes, then never again (D21, D24) on every node and through every endpoint (a direct POST after
  install); `AppSettingsWriter` (atomic, keeps other keys and the file's permissions); connection tests (timeouts,
  error text shown); starter kits (`IStarterKit`); two people installing at once; restyling (ZA-151).
- **Risk**: AGENTS.md "Installer". **Docs**: Installer.md.

### U9 Front end, SEO and languages (~4.6k)

- **Paths**: `Components/Pages/`, `Components/ContentComponents/`, `Core/Rendering/`, `Core/Seo/`, `Components/Seo/`,
  `Core/Languages/`, `ZauberCMS.Template/template/ZauberCMSTemplate.Site/` (views, layout, `Program.cs`, starter kit).
- **Focus**: `EntryPage` and the hot-path budget; `MissingView`, `NotFound404`, the error page (ZA-098); the
  restricted-content gate (ZA-110); culture and domains; sitemap generation (a hosted service; ZA-114); redirects
  (ZA-116, regex timeouts, D11); the SEO checker (SSRF, cancellation); `RenderSeoTags`; block rendering; dictionary
  lookups; what a site builder copies from the template.
- **Docs**: FrontEnd.md.

### U10 Admin shell, trees, shared UI, modal (~5.3k; split if it has grown)

- **Paths**: `Components/Admin/_Imports.razor` (the admin's `[Authorize]` gate), `Components/Admin/{Layout,Dashboards,Shared}/`, `Components/Admin/DynamicContentProperty.razor`,
  `Components/Trees/`, `Components/Shared/`, `Components/ContextMenus/SectionNavGroups/`,
  `Components/ContextMenus/ITreeContextMenu.cs`, `Components/DataSources/`, `Components/wwwroot/` (not `lib/`),
  `Components/Tailwind/`, `Components/Resources/`, `Core/Modal/` (vendored Blazored.Modal: ours to maintain),
  `Core/Sections/`.
- **Focus**: `SectionLayout` (one `<RadzenComponents />`); error boundaries (D32); tree paging (D26) and tree state
  per circuit; the context-menu runner; side panels (nesting, focus, close and cancel, child scopes); the sidebar
  resizer script; dashboard queries; accessibility of trees and menus (ZA-128).
- **Docs**: AdminUI.md.

### U11 Content section (~3.1k)

- **Paths**: `Components/Admin/ContentSection/`, `Components/ContextMenus/ContentMenus/`,
  `Components/ContextMenus/RecycleBinMenus/`.
- **Focus**: the `ContentEditor` save, publish and draft flows and its block mode; the unsaved-changes guard; the
  concurrency message; versions and compare; the copy, move, sort, restrict-access and culture/domain dialogs; list
  view paging and search; recycle-bin actions (ZA-001); double clicks on save and publish.

### U12 Block list, pickers, rich text (~4.0k; high risk)

- **Paths**: in `Components/Editors/`: `BlockListEditorProperty`, `Components/`, `Interfaces/`,
  `Models/{BlockListState,ContentEditScope,BlockListEditorSettingsModel,StagedBrowserFile,ContentPickerSettings,MediaPickerSettings,RichTextEditorSettingsModel,TinyMcePropertySettingsModel,ZauberCMSEditorSettingsModel}`,
  `ContentPickerProperty`, `MediaPickerProperty`, `MultipleMediaUpload`, `RichTextEditorProperty`, `TinyMceProperty`,
  `ZauberCMSEditorProperty`, `ToolbarItems/`, `Dialogs/`, and their components in `Settings/`.
- **Focus**: BlockListEditor.md end to end: scope commit and rollback, nested panels, previews (`BlockPreview` and its
  script), save with the page; picker order and value shape (D30); staged uploads; rich text sanitised on load, links
  and embeds (Editors.md).
- **Risk**: AGENTS.md "Block List editor & nested content". **Docs**: BlockListEditor.md, Editors.md.

### U13 Other property editors (~2.3k)

- **Paths**: the rest of `Components/Editors/` (each `*Property`, `Settings/`, `Models/`).
- **Focus**: each editor's value round-trip and format (D30, D31); its settings round-trip
  (`IContentPropertySettings<T>`); front-end value conversion; validation; accessibility; editors that load scripts or
  styles; API keys exposed to the browser (`GoogleMapsProperty`, `ApiKeysPickerProperty`); type names stored by
  `CustomComponentPicker`; `DictionaryManagerProperty`, `NavigationProperty`, `ListManagerProperty`.
- **Docs**: Editors.md.

### U14 Media and Users sections (~2.8k)

- **Paths**: `Components/Admin/MediaSection/`, `Components/ContextMenus/{MediaMenus,MediaRecycleBinMenus}/`,
  `Components/Admin/UsersSection/`.
- **Focus**: upload UI (limits, errors, progress, many files at once); image editing; move and sort; the bin and its
  usage text; the user editor (roles, password, lockout state, delete); the role editor and the permission models
  (`ContentRole`, `MediaRole`, `Permission`).

### U15 Structure and Settings sections (~2.7k)

- **Paths**: `Components/Admin/StructureSection/`, `Components/ContextMenus/Structure/`,
  `Components/Admin/SettingsSection/`, `Components/ContextMenus/SettingsMenus/`.
- **Focus**: `ContentTypeEditor` (loads on a route change only; aliases, D31; compositions from both sides; tabs; the
  property-settings editor; the change-alias dialog changes persisted names); copying and moving types; the global
  settings page (every setting has help text and a safe default); languages and dictionaries; the audit log (paging,
  filters).

### U16 Packaging, CI, template project, tests

- **Paths**: every `*.csproj`, `Directory.Build.props` and `Directory.Packages.props` (root, `tests/`,
  `ZauberCMS.Template/`), `.editorconfig`, `.gitattributes`, `global.json`, `.github/workflows/`, `release.ps1`,
  `migrations.ps1`, `ZauberCMS.Template/` (csproj and `template.json`), `ZauberCMS.Web/Program.cs` (the dev site's bootstrap: compare it
  with the template's), `ReadMe.md`, `Docs/Releasing.md`, `tests/` (about 10k lines: read it for what is and isn't
  covered and for tests that cannot fail, not for style).
- **Focus**: F4, F5; read K's results for the package, API-break and template rows (don't run them again); the test
  infrastructure (containers, the SQL Server variants, CI running the Docker tests); Standards §10 hygiene (ZA-131).

## Sweeps (`full`, or when a code lens letter is named)

Each sweep gets the pattern-count hits for its lens and reads the method around every hit. Then it builds the
inventory below for the whole codebase and checks every entry. Inventories catch what unit agents miss, because the
problem sits between units.

| Sweep | Inventory to build and check |
| --- | --- |
| S-A Async, concurrency, memory | every singleton and static field (what it holds, whether it grows, who mutates it); every event subscription and its unsubscribe; every `CreateScope`; every `Task.WhenAll`/parallel loop (shared DbContext?); every hosted service and timer (does an exception stop the host?); every piece of state that lives in one process and breaks with two servers (lenses A5); every disposable that a component or singleton owns |
| S-B Performance | every query on the front-end path (cached? key dimensions?), every cache prefix with the writes that must clear it, every admin list or tree (paged? virtualised?), every `GetInstances<T>()` call site, every per-request middleware lookup |
| S-D Security | every routable page, endpoint and controller with its gate; every input that reaches SQL, HTML, the file system, an outbound URL, a regex or a type name; every place that issues or checks a token or cookie |
| S-E Blazor | every `@rendermode` and interactive component outside the admin; every write to a `[Parameter]`; every JS interop call and its disposal; every `ErrorBoundary`; every form (SSR or interactive) and its antiforgery and `FormName` |

## Product groups (lenses G and H)

They run one after another, because they share the browser and the scratch site (product.md), and then the runtime
probes run. Each product agent walks its tasks, reads the markup and code behind each screen (the units listed, for
context: it reports UX and parity, the unit agents report the code), and owns its rows of the Umbraco map.

| Group | Covers | Tasks (product.md) | Units behind it |
| --- | --- | --- | --- |
| P1a Installer and content creation | the installer on a new template site, content tree and editor, blocks, pickers, rich text, copy/move/sort | I1, C1–C6 | U8, U11, U12 |
| P1b Content history and control | versions, recycle bin, restrict access, culture and domains, list view at scale, two editors at once, unpublished changes | C7–C12 | U4, U11 |
| P2 Media | media tree, upload, folders, image editing, media in pickers, media bin | M1–M5 | U6, U14 |
| P3 Structure and settings | content, element and composition types, property settings, languages, dictionary, global settings, SEO tools, audit log, dashboards | S1–S4, X1–X5 | U9, U13, U15 |
| P4 Users, front end, developer experience | users, roles, account pages, the public site (404, error page, culture, SEO tags, restricted pages), and building on ZauberCMS: template, a view, an editor, a section, a hook | A1–A4, F1–F4, D1–D4 | U7, U9, U10, U16 |
| R1 Runtime probe | hot-path budget, memory across circuits, cold-cache burst, upgrade from the last release | product.md "R1" | U1, U2, U9 |
| R2 Two servers (`full`, with Docker) | lens A5 on PostgreSQL + Redis | product.md "R2" | U1, U2 |
