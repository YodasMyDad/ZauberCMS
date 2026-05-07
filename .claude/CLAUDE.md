# CLAUDE.md — ZauberCMS

> Agent navigator for this repo. Read this first. Defer to [Docs/](Docs/) for deep how-tos (creating editors, services, migrations, admin pages).

## Project snapshot

**ZauberCMS** is a fully-featured .NET 10 Blazor CMS, loosely modelled on Umbraco. Current version: **`4.1.0`** (see [ZauberCMS.Core/ZauberCMS.Core.csproj:9](ZauberCMS.Core/ZauberCMS.Core.csproj#L9)).

- **Front-end** = static SSR with optional `@rendermode` opt-ins.
- **Admin** (`/admin`) = `InteractiveServer` Blazor + Radzen 8.4.0.
- Distributed as **4 NuGet packages** (`ZauberCMS.Core`, `ZauberCMS.Components`, `ZauberCMS.Routing`, `ZauberCMS`) + a **`dotnet new` template** (`ZauberCMS.Template`).
- **No CI/CD.** Releases are scripted locally via [release.ps1](release.ps1) and pushed manually to nuget.org. There is no `.github/workflows/`.
- See [ReadMe.md](ReadMe.md) for the public pitch and screenshots; see [www.zaubercms.com](https://www.zaubercms.com/) and the GitBook docs ([aptitude.gitbook.io/zaubercms](https://aptitude.gitbook.io/zaubercms)) for end-user material.

---

## 🚨 Critical rules for agents

### 1. Never recommend building from source

The [ZauberCMS.Web/](ZauberCMS.Web/) project is a **local dev runner only**. Per [ReadMe.md:41](ReadMe.md#L41): *"DO NOT USE THE SOURCE CODE TO BUILD YOUR SITE. USE THE TEMPLATE OR NUGET PACKAGE!"*. Always point users at:

```ps
dotnet new install ZauberCMS.Template@4.1.0 --force
dotnet new zaubercms -n YourSiteName
```

### 2. Schema changes need three migrations — not one

Three EF Core providers, three DbContexts, three migration folders. Use [migrations.ps1](migrations.ps1) — it generates all three migrations in one shot. Never hand-roll a single provider; the others will drift.

### 3. Render mode is load-bearing

| Surface | Render mode | UI library |
| --- | --- | --- |
| Front-end content views (`IContentView`) | **`null` = static SSR** | Consumer's choice (Bootstrap, Tailwind, etc.) |
| Admin (`/admin`) | **`InteractiveServer`** | Radzen.Blazor + Blazored.Modal |

Adding interactive code to a front-end view silently breaks SSR. To opt in, mark the specific child component `@rendermode="InteractiveServer"`. **Never use Radzen on the front-end.** The split is enforced in `App.razor` based on whether the path starts with `/admin`.

### 4. Extension discovery is by interface — wrong interface = silently invisible

Property editors, views, plugins, providers, sections, and seed data are **not registered in DI by hand**. They are reflected over by `ExtensionManager` from the assemblies registered in `AssemblyManager` at startup. A class that doesn't implement the right interface (or lives in an assembly that wasn't registered) **just doesn't appear** — there is no error. Discovery interfaces:

| Interface | What it is |
| --- | --- |
| `IContentProperty` | Property editor (admin) |
| `IContentView` | Front-end view template (alias = class name) |
| `IStartupPlugin` | Adds services to DI at boot |
| `IStorageProvider` | Media storage backend (default `DiskStorageProvider`) |
| `IEmailProvider` | Email backend (default `SmtpEmailProvider`) |
| `IExternalAuthenticationProvider` | OAuth providers (Google/Facebook/Microsoft already shipped) |
| `ISection`, `ISectionNav` | Admin nav sections and items |
| `ISeedData` | Startup data seeding |

If you add one of these and it doesn't show up, check the interface and the assembly first — not DI registration.

---

## Solution layout

| Project | Path | Role | Packed? |
| --- | --- | --- | --- |
| ZauberCMS.Core | [ZauberCMS.Core/](ZauberCMS.Core/) | Domain, services, EF Core, plugins, providers, settings, seed data | ✅ NuGet |
| ZauberCMS.Components | [ZauberCMS.Components/](ZauberCMS.Components/) | Razor class library — admin UI, property editors, dialogs, layouts | ✅ NuGet |
| ZauberCMS.Routing | [ZauberCMS.Routing/](ZauberCMS.Routing/) | Front-end catch-all routing (`@page "/{**slug}"`) | ✅ NuGet |
| ZauberCMS | [ZauberCMS/](ZauberCMS/) | Umbrella: `App.razor`, `Routes.razor`, packaged `appsettings.json`, MSBuild targets | ✅ NuGet |
| ZauberCMS.Template | [ZauberCMS.Template/](ZauberCMS.Template/) | The `dotnet new zaubercms` template package | ✅ NuGet (template) |
| ZauberCMS.Web | [ZauberCMS.Web/](ZauberCMS.Web/) | **Local dev runner only.** SQLite (`app.db`). `<ProjectReference>` to ZauberCMS. | ❌ |
| ZauberCMS.StarterSite | [ZauberCMS.StarterSite/](ZauberCMS.StarterSite/) | **Stale.** Only `bin/`/`obj/` left, not in `.sln`. **Ignore.** | ❌ |

Other top-level items: [Docs/](Docs/) (authoritative dev docs — see table at bottom), [NugetSource/](NugetSource/) (local nupkg drop / feed), [release.ps1](release.ps1), [migrations.ps1](migrations.ps1), [misc.txt](misc.txt) (notes — partly outdated).

---

## Architecture

### Bootstrap entry points

Both [ZauberCMS.Web/Program.cs](ZauberCMS.Web/Program.cs) and the template's [ZauberCMS.Template/template/ZauberCMSTemplate.Site/Program.cs](ZauberCMS.Template/template/ZauberCMSTemplate.Site/Program.cs) call:

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.AddZauberCms();      // services, DBs, identity, Radzen, plugins
var app = builder.Build();
app.AddZauberCms<App>();     // middleware, migrations, localization, routing; <App> is root
app.Run();
```

Both extension methods live in `ZauberCMS.Core/ZauberSetup.cs`. **Middleware ordering inside `app.AddZauberCms<App>()` is intentional** (image-resize before static files, culture/redirect after routing) — don't reorder casually.

### Configuration

- Root section: **`"Zauber"`** (`Constants.SettingsConfigName`).
- Bound to `ZauberSettings` ([ZauberCMS.Core/Settings/ZauberSettings.cs](ZauberCMS.Core/Settings/ZauberSettings.cs)). Major options: `DatabaseProvider`, `ConnectionString`, `RedisConnectionString`, `UploadFolderName`, `AdminDefaultLanguage`, `Email.Smtp.*`, `Plugins.StorageProvider`, `Plugins.EmailProvider`, `Identity.*`, `ImageResize.*`.
- Runtime/admin-editable settings live in the DB via `IDataService.SaveGlobalDataAsync()` → `GlobalSettings` ([ZauberCMS.Core/Settings/GlobalSettings.cs](ZauberCMS.Core/Settings/GlobalSettings.cs)): API keys, allowed file types, max upload size, admin IP whitelist, admin emails.

### Data, identity, caching, events

- Identity tables are **prefixed `Zauber*`** (e.g., `ZauberUsers`, `ZauberRoles`).
- Primary keys are **GUIDs via `NewSequentialGuid()`**.
- **Soft-delete only** (`Deleted` flag). No hard deletes anywhere — recycle-bin filters by `Deleted=true`.
- Cache: `ICacheService` ([ZauberCMS.Core/Shared/Services/DefaultCacheService.cs](ZauberCMS.Core/Shared/Services/DefaultCacheService.cs)) — uses Redis if `ZauberSettings.RedisConnectionString` is set, otherwise `IMemoryCache`. Cache keys via `.GenerateCacheKey()` extension; invalidation by **prefix**, not by exact key.
- Events: custom `AppState` with `WeakEventManager<T>` ([ZauberCMS.Core/Shared/AppState.cs](ZauberCMS.Core/Shared/AppState.cs)). Events: `OnContentChanged`, `OnUserChanged`, `OnMediaChanged`, etc. — and per-action `OnXSaved`/`OnXDeleted`.
- **No MediatR.** The codebase does not use MediatR; do not introduce `IRequest`/`IRequestHandler` patterns.

### ZauberCMS.Core folder map

| Folder | Responsibility |
| --- | --- |
| `Audit/` | Audit log (every save/delete) |
| `Content/` | Content + ContentType + Domain (services, mappings, parameters) |
| `Data/` | Three DbContexts, base class, migrations, `IZauberDbContext` |
| `Email/` | Email service + Identity email sender |
| `Extensions/` | Helper extensions (services, caching, content, security) |
| `Jobs/` | Background jobs (e.g., DailyJob) |
| `Languages/` | Languages, dictionaries, localization |
| `Media/` | Media model, validation, restricted-media middleware |
| `Membership/` | User/Role models, ASP.NET Identity wiring, OAuth providers, claims |
| `Middleware/` | `CultureMiddleware`, `RedirectMiddleware`, `RestrictedMediaMiddleware` |
| `Plugins/` | `ExtensionManager`, `AssemblyManager`, `IStartupPlugin`, plugin-related interfaces |
| `Providers/` | `IStorageProvider`, default `DiskStorageProvider`, `SmtpEmailProvider` |
| `Rendering/` | ZauberRTE rich-text rendering |
| `Sections/` | `ISection`, `ISectionNav`, `ISectionDashboard` for admin nav |
| `SeedData/` | `ISeedData` startup seeding |
| `Seo/` | SEO redirects service |
| `Settings/` | `ZauberSettings`, `GlobalSettings` |
| `Shared/` | `AppState`, `DefaultCacheService`, `ValidateService`, `ICacheService` |
| `Tags/` | Tagging system |

---

## Data layer (multi-provider)

| Provider | DbContext | Migrations folder |
| --- | --- | --- |
| SQL Server | `ZauberDbContext` ([ZauberCMS.Core/Data/ZauberDbContext.cs](ZauberCMS.Core/Data/ZauberDbContext.cs)) | [ZauberCMS.Core/Data/Migrations/SqlServer/](ZauberCMS.Core/Data/Migrations/SqlServer/) |
| SQLite | `SqliteZauberDbContext` ([ZauberCMS.Core/Data/SqliteZauberDbContext.cs](ZauberCMS.Core/Data/SqliteZauberDbContext.cs)) | [ZauberCMS.Core/Data/Migrations/SqLite/](ZauberCMS.Core/Data/Migrations/SqLite/) |
| PostgreSQL | `PostgreSqlZauberDbContext` ([ZauberCMS.Core/Data/PostgreSqlZauberDbContext.cs](ZauberCMS.Core/Data/PostgreSqlZauberDbContext.cs)) | [ZauberCMS.Core/Data/Migrations/PostgreSql/](ZauberCMS.Core/Data/Migrations/PostgreSql/) |

All three inherit `ZauberDbContextBase` (an `IdentityDbContext<User, Role, Guid, ...>` — see `ZauberCMS.Core/Data/ZauberDbContextBase.cs`). Entity configurations are auto-applied via `modelBuilder.ApplyConfigurationsFromAssembly(...)`.

Provider chosen at startup by `ZauberSettings.DatabaseProvider`:
- `"Sqlite"` (default) → connection string typically `DataSource=app.db;Cache=Shared`
- `"SqlServer"` → `Server=...;Database=...;Trusted_Connection=True;TrustServerCertificate=True;`
- `"PostgreSql"` → `Host=...;Port=5432;Database=...;Username=...;Password=...;`

**To make a schema change**: edit the entity → run [migrations.ps1](migrations.ps1) → it produces three migrations in the right folders. **Defer to [Docs/efcore.md](Docs/efcore.md)** for the procedure and constraints (key rule: *"DO NOT add new tables or properties unless absolutely necessary."*).

---

## Services

Pattern: `IXService` interface + `XService` implementation. Constructor injects `IServiceScopeFactory`, `ICacheService`, `IOptions<ZauberSettings>`, `ExtensionManager`, `AppState`, logger. Every method takes a **parameter class** (e.g. `GetContentParameters`, `SaveContentParameters`) — not loose arguments.

Representative services:

| Service | What |
| --- | --- |
| `IContentService` / `ContentService` | Most complex. Content + ContentType CRUD, queries, copy, versioning. |
| `IMembershipService` / `MembershipService` | Wraps `UserManager<User>` + `SignInManager`. |
| `IMediaService` / `MediaService` | Orchestrates `IStorageProvider`. |
| `ILanguageService` | Languages + dictionary entries. |
| `ITagService`, `ISeoService`, `IDataService`, `IEmailService` | Self-explanatory. |

Common patterns: queries built as `IQueryable` → `.GenerateCacheKey()` → `cacheService.GetSetCachedItemAsync()`. Cache invalidation via `cacheService.ClearCachedItemsWithPrefix(...)`.

**Defer to [Docs/Services.md](Docs/Services.md)** for parameter classes, query patterns, caching strategy, and the rules for adding a new service.

---

## Front-end (Routing + EntryPage)

- **Catch-all**: [ZauberCMS.Routing/CatchAll.razor](ZauberCMS.Routing/CatchAll.razor) — `@page "/{**slug}"`. Must remain in `ZauberCMS.Routing`, which is loaded **last** by `AssemblyManager` so this catch-all doesn't intercept other routes.
- **URL → content lookup**: `EntryPage.razor` ([ZauberCMS.Components/Pages/EntryPage.razor](ZauberCMS.Components/Pages/EntryPage.razor)) first checks `CultureMiddleware.GetEntryModel(HttpContext)` (avoids a double DB hit), then falls back to `ContentService.GetContentFromRequestAsync()`.
- **View resolution**: `ExtensionManager.GetInstances<IContentView>()` returns all view classes. The view is selected by matching `Content.ViewComponent` (the alias set in admin) against the **class name**. Example: a class named `HomeView` matches alias `"HomeView"`.
- **Renaming a view class breaks rendering** unless you also update the content item's `ViewComponent` in the admin.
- A view receives `[Parameter] public Content? Content` and `[Parameter] public Dictionary<string, string>? LanguageKeys`. Read property values via `Content.GetValue<T>("PropertyAlias")`. Read block lists via `await Content.GetBlocks("BlockListPropertyAlias", ContentService)`.
- Per-content-type templates live in the **consumer's project** (e.g. [ZauberCMS.Web/Pages/HomeView.razor](ZauberCMS.Web/Pages/HomeView.razor) for the dev runner).

---

## Admin UI

- **Path**: `/admin` (centralised in `Urls.AdminBaseUrl`).
- **Layout**: [ZauberCMS.Components/Admin/Layout/SectionLayout.razor](ZauberCMS.Components/Admin/Layout/SectionLayout.razor) — wraps `RadzenLayout` / `RadzenSidebar` / `RadzenBody`. Also enforces the **admin IP whitelist** (`GlobalSettings.AllowedAdminIpAddress`) — non-whitelisted users are signed out on layout init.
- **UI stack**: Radzen.Blazor `8.4.0`, Blazored.Modal `7.3.1`, TinyMCE `2.2.1` + `ZauberCMS.RTE 2.1.0`, BlazorMonaco `3.4.0`.
- **Sections**: discovered via `ISection`/`ISectionNav`. Built-ins: ContentSection, MediaSection, UsersSection, SettingsSection, StructureSection.
- **Property editors**: [ZauberCMS.Components/Editors/](ZauberCMS.Components/Editors/) — each implements `IContentProperty` (`Name`, `Alias`, `Icon`, `SettingsComponent`, etc.) and has a matching settings component in [ZauberCMS.Components/Editors/Settings/](ZauberCMS.Components/Editors/Settings/) backed by a model in [ZauberCMS.Components/Editors/Models/](ZauberCMS.Components/Editors/Models/). Settings are **stored as a JSON string in the DB** — new editors must round-trip via `Settings.FromJson<TModel>()`.

**Defer to [Docs/AdminUI.md](Docs/AdminUI.md)** for adding pages/sections, and **[Docs/Editors.md](Docs/Editors.md)** for adding/modifying property editors.

---

## NuGet packaging — the four core packages

All four set `<GeneratePackageOnBuild>true</GeneratePackageOnBuild>`, so a plain `dotnet build ZauberCMS.sln -c Release` produces all four `.nupkg` files. **No separate `dotnet pack` is needed for the core packages.**

Each csproj also defines a shared MSBuild target ([ZauberCMS.Core/ZauberCMS.Core.csproj:64-70](ZauberCMS.Core/ZauberCMS.Core.csproj#L64-L70)):

```xml
<Target Name="CopyNuGetPackage" AfterTargets="Pack">
    <PropertyGroup>
        <PackageOutputDir>$(OutputPath)..\</PackageOutputDir>
        <GeneratedPackage>$(PackageOutputDir)\$(PackageId).$(Version).nupkg</GeneratedPackage>
    </PropertyGroup>
    <Copy SourceFiles="$(GeneratedPackage)" DestinationFolder="$(LocalNugetSource)" />
</Target>
```

…with `<LocalNugetSource>$(MSBuildProjectDirectory)\..\NugetSource</LocalNugetSource>`. **Result**: nupkgs land in [NugetSource/](NugetSource/) at repo root automatically as part of a Release build.

Shared package metadata (all four):
- `Authors=Lee Messenger`
- `PackageLicenseExpression=MIT`
- `RepositoryUrl=https://github.com/YodasMyDad/ZauberCMS`
- `PackageProjectUrl=https://github.com/YodasMyDad/ZauberCMS`
- `PackageTags=Web`

`ZauberCMS.Components`, `ZauberCMS.Routing`, and `ZauberCMS` use `Sdk="Microsoft.NET.Sdk.Razor"` and set `<StaticWebAssetBasePath>_content/$(PackageId)</StaticWebAssetBasePath>`.

### Inter-package PackageReference chain (rewritten by release.ps1)

```
ZauberCMS.Components → ZauberCMS.Core
ZauberCMS.Routing    → ZauberCMS.Components
ZauberCMS            → ZauberCMS.Components + ZauberCMS.Routing
```

Each Razor csproj **also has a `<ProjectReference>`** to its sibling for local development — both refs coexist. Don't be confused by seeing both. Example from [ZauberCMS.Components/ZauberCMS.Components.csproj:48-53](ZauberCMS.Components/ZauberCMS.Components.csproj#L48-L53):

```xml
<ItemGroup>
  <ProjectReference Include="..\ZauberCMS.Core\ZauberCMS.Core.csproj" />
</ItemGroup>
<ItemGroup>
  <PackageReference Include="ZauberCMS.Core" Version="4.1.0" />
</ItemGroup>
```

### Extras packed into `ZauberCMS.csproj`

- `appsettings.json` → `build/settings.json` (so consumers get sane defaults)
- `ZauberCMS.targets` → `build/ZauberCMS.targets`
- `../ReadMe.md` → package root

---

## The .NET template (`dotnet new zaubercms`)

- **Project**: [ZauberCMS.Template/](ZauberCMS.Template/) — packs the [template/](ZauberCMS.Template/template/) folder as the `content/` of the nupkg.
- **csproj is non-compiling** ([ZauberCMS.Template/ZauberCMS.Template.csproj:14-18](ZauberCMS.Template/ZauberCMS.Template.csproj#L14-L18)):
  - `<PackageType>Template</PackageType>`
  - `<PackageVersion>4.1.0</PackageVersion>` (note: `PackageVersion`, not `Version`)
  - `<IncludeContentInPack>true</IncludeContentInPack>`
  - `<IncludeBuildOutput>false</IncludeBuildOutput>` — no compiled assemblies
  - `<ContentTargetFolders>content</ContentTargetFolders>`
  - `<NoWarn>$(NoWarn);NU5128</NoWarn>` (suppresses "no dependency" warning)
  - `<NoDefaultExcludes>true</NoDefaultExcludes>`
  - `<Compile Remove="**\*" />` — nothing compiled
- **Template metadata**: [ZauberCMS.Template/template/.template.config/template.json](ZauberCMS.Template/template/.template.config/template.json) — `shortName: "zaubercms"`, `sourceName: "ZauberCMSTemplate"` (replaced with whatever the user passes via `-n`).
- **Shipped scaffold**: [ZauberCMS.Template/template/ZauberCMSTemplate.Site/](ZauberCMS.Template/template/ZauberCMSTemplate.Site/) — minimal `Program.cs`, full `appsettings.json` (defaults to SQLite + the standard Zauber config tree), `Properties/launchSettings.json`, empty `wwwroot/.gitkeep`. The csproj has a single `<PackageReference Include="ZauberCMS" Version="4.1.0" />` (release.ps1 keeps this pinned to the released version).

The note in [misc.txt:49](misc.txt#L49) about renaming the nupkg to `.zip` to inject `.template.config/` is **historical/superseded** — the current csproj packs the template properly.

---

## Release flow ([release.ps1](release.ps1))

```ps
.\release.ps1 -NewVersion 4.2.0
```

What it does:

1. Reads current version from `ZauberCMS.Core.csproj`.
2. Prompts for the new version (or uses `-NewVersion`). Format `x.y.z` or `x.y.z-suffix`.
3. Updates `<Version>` in the four core csproj files **and** `<PackageVersion>` in the template csproj **and** `<Version>` in `ZauberCMSTemplate.Site.csproj` — **6 csproj files in total**.
4. Rewrites internal `PackageReference` versions with regex ([release.ps1:41-55](release.ps1#L41-L55)):
   - `ZauberCMS.Components` → bumps `ZauberCMS.Core` ref
   - `ZauberCMS.Routing` → bumps `ZauberCMS.Components` ref
   - `ZauberCMS` → bumps `ZauberCMS.Components` and `ZauberCMS.Routing` refs
   - `ZauberCMSTemplate.Site` → bumps `ZauberCMS` ref
   This is **critical** — it means `dotnet new zaubercms` always scaffolds a project that depends on the **released NuGet**, not on a project reference.
5. Runs `dotnet build ZauberCMS.sln -c Release` → produces 4 nupkgs (via `GeneratePackageOnBuild`) + the `CopyNuGetPackage` MSBuild target lands them in [NugetSource/](NugetSource/).
6. Runs `dotnet pack -c Release` inside `ZauberCMS.Template/` → produces `ZauberCMS.Template.<ver>.nupkg` in `bin/Release/`, then `Move-Item`s it to [NugetSource/](NugetSource/) (the template csproj has no `CopyNuGetPackage` target, so the script does it manually).

After release.ps1 finishes, [NugetSource/](NugetSource/) contains all 5 packages (e.g. `ZauberCMS.4.1.0.nupkg`, `ZauberCMS.Components.4.1.0.nupkg`, `ZauberCMS.Core.4.1.0.nupkg`, `ZauberCMS.Routing.4.1.0.nupkg`, `ZauberCMS.Template.4.1.0.nupkg`).

### Pushing to nuget.org

**Manual.** No `nuget push` is automated. No `.github/workflows/`, no `NUGET_API_KEY` reference anywhere in the repo. Push from the [NugetSource/](NugetSource/) folder, e.g.:

```ps
dotnet nuget push NugetSource\ZauberCMS.Core.4.1.0.nupkg -k <API_KEY> -s https://api.nuget.org/v3/index.json
# repeat for the other four
```

---

## Dev workflow

- **Run locally**: open [ZauberCMS.sln](ZauberCMS.sln), run `ZauberCMS.Web` (SQLite, `app.db`). Visit `/admin` and register the first user.
- **Test a new local NuGet build**: `dotnet nuget locals all --clear` to dodge cache, then point a test consumer at the [NugetSource/](NugetSource/) folder as a feed.
- **Test the template locally**:
  ```ps
  cd ZauberCMS.Template/template
  dotnet new install ./
  dotnet new zaubercms -n MyTest
  dotnet new uninstall ./   # to remove
  ```
- **Sample DB tracked in git**: [ZauberCMS.Web/app.db](ZauberCMS.Web/app.db) is committed. `git status` will show it modified after almost every dev run — **don't commit unless intentional**.
- Branches: default branch is `main-static` (PRs target this; `Closes #N` in commits to it auto-closes issues). `master` still exists on the remote as a legacy branch from an older version — don't push there. Local `origin/HEAD` may still point at `origin/master` cosmetically; run `git remote set-head origin -a` to refresh if it's misleading.

---

## Reference: existing `/Docs` files

| Doc | Use when… |
| --- | --- |
| [Docs/developer.md](Docs/developer.md) | Establishing tone for code/answers (terse, expert reader, no high-level theory). |
| [Docs/AdminUI.md](Docs/AdminUI.md) | Touching `/admin` pages, sections, layouts, Radzen patterns. |
| [Docs/Editors.md](Docs/Editors.md) | Adding/modifying property editors (`IContentProperty`, settings, models, dialogs). |
| [Docs/efcore.md](Docs/efcore.md) | Any DB schema change. Contains the "do not add tables unless absolutely necessary" rule. |
| [Docs/Services.md](Docs/Services.md) | Adding services, parameter classes, query/cache patterns. |

These are Cursor-style rule files (`alwaysApply: true` frontmatter) but they are authoritative dev docs regardless of editor.

---

## Gotchas (compact list)

- **Static SSR vs InteractiveServer split** — covered in Critical rules. Don't put Radzen on the front-end.
- **`CatchAll.razor` placement is load-bearing** — must stay in `ZauberCMS.Routing` (loaded last by `AssemblyManager`).
- **View alias = class name** — renaming a view class breaks `Content.ViewComponent` lookups.
- **Property editor `Settings` are JSON strings in the DB** — new editors must follow the `Settings.FromJson<TModel>()` pattern, otherwise the settings UI won't round-trip.
- **`AppState` uses `WeakEventManager<T>`** — singletons referencing scoped components are safe from leaks, but missed unsubscribes mean events silently stop firing for collected components.
- **`ZauberSetup` runs an async startup query synchronously** (`GetAwaiter().GetResult()` to populate supported cultures before `RequestLocalization`). Tread carefully when editing the bootstrap path.
- **Identity password rules and admin layout components are configurable via `ZauberSettings.Identity`** — never hard-code them.
- **Login endpoint has rate limiting** (10 attempts / 5 minutes). Tests/scripts that hammer login will lock out.
- **Middleware ordering inside `app.AddZauberCms<App>()` is intentional** — image-resize before static files; culture/redirect after routing. Don't reorder casually.
- **Identity tables are `Zauber*` prefixed** — when writing raw SQL (rare), it's `ZauberUsers`, not `AspNetUsers`.
- **`misc.txt`** has some outdated notes (the template-as-zip workaround is superseded).
- **`ZauberCMS.StarterSite/`** is stale (only `bin/`/`obj/` remain). Don't reference it; the equivalent live scaffold lives in [ZauberCMS.Template/template/ZauberCMSTemplate.Site/](ZauberCMS.Template/template/ZauberCMSTemplate.Site/).
