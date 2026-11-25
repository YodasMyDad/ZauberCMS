# AGENTS.md — ZauberCMS

Single source of truth for AI agents and new contributors. Read this first. `CLAUDE.md` only imports this file.
Deep detail lives in [Docs/](Docs/) — open the one doc that matches the task, not all of them.

## What this is

A Blazor-native CMS for .NET 10, inspired by Umbraco. Goal: **simple, modular, extended in plain C#/Razor, no front-end
build chain for site builders.** Shipped as NuGet packages + a `dotnet new zaubercms` template; consumers never build
from this source ([ReadMe.md](ReadMe.md)).

| Surface | Render mode | UI |
| --- | --- | --- |
| Front-end site (`IContentView`) | static SSR; per-component `@rendermode` opt-in | consumer's choice — **never Radzen** |
| Admin `/admin` | `InteractiveServer` | Radzen.Blazor + Blazored.Modal (+ Tailwind for admin CSS) |
| Account pages (`/Account/*`, in Components) | static SSR | layout via `IAccountLayout` |

`ZauberCMS/Components/App.razor` picks the render mode: `/admin` (or no `HttpContext`) → InteractiveServer, else SSR.
Adding interactive code to an SSR view silently does nothing.

## Solution map

| Project | Role | Packed |
| --- | --- | --- |
| `ZauberCMS.Core` | Domain, services, EF Core (3 providers), plugins, providers, settings, seed data, `ZauberSetup.cs` | NuGet |
| `ZauberCMS.Components` | Razor class lib: admin UI, property editors, dialogs, trees, context menus, account pages | NuGet |
| `ZauberCMS.Routing` | Front-end catch-all `CatchAll.razor` (`@page "/{**slug}"`) — **must load last** | NuGet |
| `ZauberCMS` | Umbrella: `App.razor`, `Routes.razor`, packaged `appsettings.json`, `ZauberCMS.targets` | NuGet |
| `ZauberCMS.Template` | `dotnet new zaubercms` template (non-compiling csproj) | NuGet (template) |
| `ZauberCMS.Web` | **Local dev runner + sample site only** (SQLite `app.db`, tracked — don't commit dev changes to it) | no |

Core folders are by feature: `<Feature>/{Interfaces,Services,Parameters,Models,Mapping}` (Content, Media, Membership,
Languages, Tags, Seo, Audit, Data, Email, …). Copy the nearest existing feature rather than inventing a layout.

Bootstrap (both `ZauberCMS.Web/Program.cs` and the template): `builder.AddZauberCms(params Assembly[] extra)` then
`app.AddZauberCms<App>()` — both in `ZauberCMS.Core/ZauberSetup.cs`. At startup it **auto-runs EF migrations**, runs
every `ISeedData`, and does one intentional sync-over-async culture query. Middleware order there is deliberate
(restricted-media → image-resize → static files → routing → redirect → culture → rate limiter → authz → antiforgery);
don't reorder without a reason written in the commit.

## Non-negotiable rules

1. **Schema change = three migrations.** SQL Server, SQLite, PostgreSQL each have a DbContext and a migrations folder.
   Run `./migrations.ps1 -MigrationName X` from the repo root; commit all three + snapshots. Never hand-edit or hand-roll
   one provider. Pending-model-change warnings are suppressed, so drift is **silent**. Don't add tables/columns unless
   unavoidable. → [Docs/Data.md](Docs/Data.md)
2. **Public types are a public API.** Every public interface, parameter class, model and component ships in NuGet to
   consumer sites. No breaking change without `[Obsolete]` first and a major version. **Type full names are persisted
   data**: views (`Content.ViewComponent`), property editors (`PropertyType.Component`) and fallback views in settings
   are stored as `Type.FullName` — renaming a class *or its namespace* breaks existing sites.
3. **Extensions are discovered, not registered.** `ExtensionManager` reflects over public, non-abstract types in the
   scanned assemblies (Core, Components, host entry assembly, `AddZauberCms(extra)` assemblies, Routing last).
   Wrong interface or unscanned assembly = silently invisible; check those before touching DI. Instances are built with
   `ActivatorUtilities` (ctor DI works), except `IStartupPlugin`/`IExternalAuthenticationProvider` which are created
   before DI exists and **need a parameterless ctor**.
4. **Soft delete is partial.** Only `Content`, `Media`, `User` have `Deleted`. There are **no global query filters** —
   every query on those three must filter `!x.Deleted` (or pass `IncludeDeleted` for recycle-bin paths). Everything
   else is hard-deleted; recycle-bin purge is a hard delete.
5. **Writes go through `SaveChangesAndLog`** (runs `IBeforeEntitySave<T>`/`IAfterEntitySave<T>` hooks and clears the
   `typeof(T).Name` cache prefix). Clear related prefixes yourself. → [Docs/Services.md](Docs/Services.md)
6. **No MediatR / CQRS libraries.** Services are `IXService` + parameter class + `HandlerResult<T>`.
7. **Follow [Docs/Standards.md](Docs/Standards.md)** for every change. It defines the engineering bar and the
   Definition of Done below.

## Extension points (discovery interfaces)

| Interface | Purpose |
| --- | --- |
| `IContentView` | Front-end page view (needs `@layout`) |
| `IContentBlockView` / `IContentBlockPreview` | Block render on site / preview in admin (matched by `ContentTypeAlias`) |
| `IContentProperty` (+ `IContentPropertySettings<T>`) | Property editor (+ its settings UI) |
| `ISection`, `ISectionNavGroup`, `ISectionNav`, `ISectionNavGroupAction`, `ISectionDashboard` | Admin sections, nav groups, nav items, group "…" actions, dashboard tabs |
| `ITreeContextMenu` | Admin tree right-click menu items |
| `ICustomContentComponent`, `IDataListSource`, `ISeoCheck`, `IAccountLayout` | Custom content tabs, list-editor data sources, SEO checks, account-page layout |
| `IStorageProvider`, `IEmailProvider` | Media storage / email backend (selected by `Zauber:Plugins:*`) |
| `IExternalAuthenticationProvider`, `IStartupPlugin` | OAuth providers, DI/startup hooks (parameterless ctor) |
| `ISeedData` | Runs every boot after migrations |
| `IValidate<T>`, FluentValidation `IValidator<T>` | Admin-side validation |
| `IBeforeEntitySave<T>`, `IAfterEntitySave<T>` | Save hooks (before can veto) |

`IContentFinder` / `ContentFinderPipeline` and the MVC `RenderBlocksViewComponent` path are currently unwired (routes
commented out in `ZauberSetup.cs`) — don't build on them without discussing.

## Configuration & cross-cutting

- Config section `"Zauber"` → `ZauberSettings` (`ZauberCMS.Core/Settings/ZauberSettings.cs`). Admin-editable runtime
  settings → `GlobalSettings`, stored as `GlobalData` alias `"GlobalSettings"`, read via `dataService.GetGlobalSettings()`.
- Cache: `ICacheService` (Redis when `RedisConnectionString` set, else `IMemoryCache`). Keys `TypeName-<sha256 of SQL>`;
  invalidation by prefix (`StartsWith`). Key registry is per-process — prefix clears are not multi-node safe with Redis.
- Events: singleton `AppState` with `WeakEventManager` (`OnXChanged/Saved/Deleted` for Content, ContentType, User,
  Media). Handlers fire on the publisher's thread in every circuit → marshal with `InvokeAsync`.
- Identity tables are `Zauber*`-prefixed; migrations history table `ZauberMigrations`. Keys are sequential GUIDs.
- Login-like pages use rate-limit policy `"login"` (per-IP 10 / 5 min); Identity lockout 6 attempts / 5 min.
- Admin is gated by `[Authorize(Roles = Admin)]` in `ZauberCMS.Components/Admin/_Imports.razor` (that folder only — admin
  pages elsewhere must add it) and the `GlobalSettings.AllowedAdminIpAddress` check in `SectionLayout.razor`.

## High-risk areas — read the doc before editing

- **Block List editor & nested content** (`Editors/BlockList*`, `ContentEditor.razor` block handling, `RenderBlock` /
  `shadow-dom.js`, block snapshots). Changes bubble up through nested modals via callbacks and are persisted only on the
  root save, one block at a time with no overall transaction. Known data-loss bugs (cancel leaks, multi-property merge,
  draft saves going live) and a planned redesign (edit session + single `SaveContentTreeAsync` + static previews).
  **Don't extend the callback pattern or copy it into new editors.** → [Docs/BlockListEditor.md](Docs/BlockListEditor.md)
- **Soft delete / recycle bin** — filter defaults on Get/Query parameters; flipping one breaks callers silently (ZA-001).
- **Bootstrap** (`ZauberSetup.cs`) — assembly order, middleware order, auto-migrate, seeders.
- **Migrations** — three providers, silent drift.

Open findings with IDs: [.audit/report.md](.audit/report.md).

## Definition of Done (every change)

1. `dotnet build ZauberCMS.sln -c Release` succeeds with **no new warnings**.
2. Schema touched → three migrations via `migrations.ps1`. Public API touched → no unannounced break (rule 2).
3. Tests added/updated for changed behaviour once a test project exists (none yet — see Standards.md; until then state
   how it was verified).
4. **Docs updated in the same change** when behaviour, an extension point, a convention or a fact in this file changes.
   A doc that disagrees with code is a bug. Never write versions into docs — point at the csproj.
5. Self-review with the `doublecheck` skill; larger changes or before a release run the `audit` skill.

## Where to look

| Task | Read |
| --- | --- |
| Engineering standards, Blazor/.NET patterns, testing, security bar | [Docs/Standards.md](Docs/Standards.md) |
| Services, parameter classes, caching, results, audit, events | [Docs/Services.md](Docs/Services.md) |
| Entities, mappings, migrations, provider gotchas | [Docs/Data.md](Docs/Data.md) |
| Front-end views, layouts, blocks, helpers, routing | [Docs/FrontEnd.md](Docs/FrontEnd.md) |
| Admin sections, nav, trees, context menus, dialogs, dashboards | [Docs/AdminUI.md](Docs/AdminUI.md) |
| Property editors and their settings | [Docs/Editors.md](Docs/Editors.md) |
| Block list editor (nesting, previews, persistence, versions) | [Docs/BlockListEditor.md](Docs/BlockListEditor.md) |
| CI, releases, NuGet packaging, the template | [Docs/Releasing.md](Docs/Releasing.md) |
| Full codebase audit (runbook) / latest findings | [.claude/skills/audit/SKILL.md](.claude/skills/audit/SKILL.md) / [.audit/report.md](.audit/report.md) |

## Working agreements

- Default branch `main-static`; **every push to it publishes to nuget.org** (CI bumps the patch and commits the bump
  back). Commit/push only when asked; never push `master` (legacy). Pull before starting work.
- Tone: terse, expert reader, real code over theory. Never rename existing public members/types without asking.
- Keep context lean: read by line range, delegate broad searches, keep build output to errors/warnings.
