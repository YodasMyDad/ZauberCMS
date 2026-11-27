# AGENTS.md — ZauberCMS

Single source of truth for AI agents and new contributors. Read this first. `CLAUDE.md` only imports this file.
**Active work: the 2026 rework on branch `rework-2026` — read [Docs/Roadmap.md](Docs/Roadmap.md) next** (status, owner
decisions, phase order). Don't push or merge to `main-static` during the rework.
Deep detail lives in [Docs/](Docs/) — open the one doc that matches the task, not all of them.

## What this is

A Blazor-native CMS for .NET 10, inspired by Umbraco. Goal: **simple, modular, extended in plain C#/Razor, no front-end
build chain for site builders.** Shipped as NuGet packages + a `dotnet new zaubercms` template; consumers never build
from this source ([ReadMe.md](ReadMe.md)).

| Surface | Render mode | UI |
| --- | --- | --- |
| Front-end site (`IContentView`) | static SSR; per-component `@rendermode` opt-in | consumer's choice — **never Radzen** |
| Admin `/admin` | `InteractiveServer`, not prerendered, always a full page load | Radzen.Blazor + Blazored.Modal (vendored source in `ZauberCMS.Components/Modal/`, not a package; nested side panels) (+ Tailwind for admin CSS) |
| Account pages (`/Account/*`, in Components) | static SSR | layout via `IAccountLayout` |

`ZauberCMS/Components/App.razor` picks the render mode: `/admin` (or no `HttpContext`) → InteractiveServer without
prerendering, else SSR. Admin parts that run code they do not own sit in an `AdminErrorBoundary` (Roadmap D32).
Adding interactive code to an SSR view silently does nothing.

## Core principle: pluggable and updatable (owner, 2026-11-25)

ZauberCMS is a CMS other people build on. A site owner must be able to **add to it, customise it and replace parts of
it from their own project, and then update the ZauberCMS packages without redoing that work.** Weigh every change,
in every phase of the rework, against this:

- **Extend from outside.** New behaviour is reachable from a consumer project in plain C#/Razor: implement a
  discovery interface (table below), register a service, or set a config key. If a feature can only be changed by
  editing ZauberCMS source, it is not finished.
- **Discovery by reflection is the mechanism, so treat it as a contract.** `ExtensionManager` finds extension types
  by interface in the scanned assemblies. Don't hard-code lists of the built-in implementations, don't special-case
  "our" types over a consumer's, and don't make a new extension point that needs manual registration when discovery
  would do. Type full names are stored in the database (rule 2).
- **Replaceable defaults.** Built-in behaviour (storage, email, sanitiser, layouts, views, checks) sits behind an
  interface or a setting with a working default, so a site can swap it without forking.
- **Updates must be safe.** Consumers update by bumping the package version. No breaking public API, persisted
  format or config key without the `[Obsolete]` path (rule 2); new settings need a default that keeps existing
  sites working; a behaviour change that a consumer could notice goes in the release notes.
- **Security and structure changes include the plugin author's path.** When something is locked down (for example
  `/admin/**` authorisation), say in the docs what a plugin page or extension has to do to keep working.
- When a design choice trades internal neatness against ease of extending or updating, extending and updating win.
  If a change would make either harder, stop and raise it with the owner.

## Solution map

| Project | Role | Packed |
| --- | --- | --- |
| `ZauberCMS.Core` | Domain, services, EF Core (3 providers), plugins, providers, settings, seed data, installer, `ZauberSetup.cs`; **no UI library** (no Radzen, no modal) | NuGet |
| `ZauberCMS.Components` | Razor class lib: admin UI, property editors, dialogs, trees, context menus, account pages; Radzen, the vendored modal, and the UI-typed contracts that keep Core namespaces (`Core/` folder, D51) | NuGet |
| `ZauberCMS.Routing` | Front-end catch-all `CatchAll.razor` (`@page "/{**slug}"`) — **must load last** | NuGet |
| `ZauberCMS` | Umbrella: `App.razor`, `Routes.razor`, packaged `appsettings.json`, `ZauberCMS.targets` | NuGet |
| `ZauberCMS.Template` | `dotnet new zaubercms` template (non-compiling csproj); ships the starter kit and its views | NuGet (template) |
| `ZauberCMS.Web` | **Local dev runner + sample site only** (SQLite `app.db`, tracked: sample content, **no accounts** — a clone runs the installer to create its administrator; don't commit dev changes to it) | no |
| `tests/ZauberCMS.Core.Tests`, `tests/ZauberCMS.Components.Tests` | xUnit tests (services on in-memory SQLite through the real bootstrap; component models) | no |

Shared build settings and the four packages' one `<Version>` are in `Directory.Build.props`; every package version
is in `Directory.Packages.props` (a csproj `PackageReference` has no `Version`). `ZauberCMS.Template/` is outside
both: the template site keeps explicit versions, because `dotnet new` copies it to consumers.

Core folders are by feature: `<Feature>/{Interfaces,Services,Parameters,Models,Mapping}` (Content, Media, Membership,
Languages, Tags, Seo, Audit, Data, Email, …). Copy the nearest existing feature rather than inventing a layout.

Bootstrap (both `ZauberCMS.Web/Program.cs` and the template): `builder.AddZauberCms(params Assembly[] extra)` then
`app.AddZauberCms<App>()` — both in `ZauberCMS.Core/ZauberSetup.cs`. At startup it **auto-runs EF migrations**, runs
every `ISeedData`, works out whether the site is installed, and makes two intentional sync-over-async calls (the database
initialise and the culture query).
A site whose `Zauber:DatabaseProvider` / `ConnectionString` are empty starts **without a database** and serves only
the installer (→ [Docs/Installer.md](Docs/Installer.md)). Middleware order there is deliberate
(forwarded headers when enabled → error page outside Development (D33) → installer → authentication → media security headers → restricted-media →
image-resize → static files → routing → redirect → culture → admin access → rate limiter → authz → antiforgery);
don't reorder without a reason written in the commit.

## Non-negotiable rules

1. **Schema change = three migrations.** SQL Server, SQLite, PostgreSQL each have a DbContext and a migrations folder.
   Run `./migrations.ps1 -MigrationName X` from the repo root; commit all three + snapshots. Never hand-edit or hand-roll
   one provider. Pending-model-change warnings are suppressed, so drift is **silent**. Don't add tables/columns unless
   unavoidable. → [Docs/Data.md](Docs/Data.md)
2. **Public types are a public API.** Every public interface, parameter class, model and component ships in NuGet to
   consumer sites. No breaking change without `[Obsolete]` first and a major version. (During the rework everything ships as one
   v5 major, so a break is allowed when it buys something and is listed in the release notes — Roadmap D18.) **Type full names are persisted
   data**: views (`Content.ViewComponent`), property editors (`PropertyType.Component`) and fallback views in settings
   are stored as `Type.FullName` — renaming a class *or its namespace* breaks existing sites. What is public and what
   is internal follows [Docs/Standards.md](Docs/Standards.md) §1 "What is public" (D53); `PublicApiTests` pins the
   list of public types, so making a type public is a decision written into its snapshot.
3. **Extensions are discovered, not registered.** `ExtensionManager` reflects over public, non-abstract types (make
   extension types public — only the bootstrap path also sees non-public ones) in the
   scanned assemblies (Core, Components, host entry assembly, `AddZauberCms(extra)` assemblies, Routing last).
   Wrong interface or unscanned assembly = silently invisible; check those before touching DI. A non-public extension
   type in a site or plugin assembly, and what the bootstrap skips (types that cannot load, no parameterless ctor),
   are logged as start-up warnings (category `ZauberCMS.Startup`). Instances are built with
   `ActivatorUtilities` (ctor DI works), except `IStartupPlugin`/`IExternalAuthenticationProvider` which are created
   before DI exists and **need a parameterless ctor**.
4. **Soft delete is partial.** Only `Content`, `Media`, `User` have `Deleted`. There are **no global query filters** —
   every query on those three must filter `!x.Deleted` (or pass `IncludeDeleted` for recycle-bin paths). Everything
   else is hard-deleted; recycle-bin purge is a hard delete. Content and Media recycle, restore and purge act on the
   whole subtree; a media purge also removes the files, and recycled media is not served
   (→ [Docs/Services.md](Docs/Services.md) "Deletes").
5. **Writes go through `SaveChangesAndLog`**: stage the whole change (audit row included), then call it once. It runs
   the `IBeforeEntitySave<T>`/`IAfterEntitySave<T>` hooks for every row the save writes, of every type, and one veto
   stops the whole change (Roadmap D28); it clears the cache prefix of each type written. Never `SaveChangesAsync`
   before it: the hooks would see those rows `Unchanged`. Clear related prefixes yourself.
   → [Docs/Services.md](Docs/Services.md)
6. **No MediatR / CQRS libraries.** Services are `IXService` + parameter class + `HandlerResult<T>`.
7. **Follow [Docs/Standards.md](Docs/Standards.md)** for every change. It defines the engineering bar and the
   Definition of Done below.

## Extension points (discovery interfaces)

| Interface | Purpose |
| --- | --- |
| `IContentView` | Front-end page view (needs `@layout`) |
| `IContentFinder` | Which page an address shows when no page has its URL (`/products/{sku}`); ordered by `SortOrder`, first found wins |
| `IContentBlockView` / `IContentBlockPreview` | Block render on site / preview in admin (matched by `ContentTypeAlias`) |
| `IContentProperty` (+ `IContentPropertySettings<T>`) | Property editor (+ its settings UI) |
| `INestedContentReferenceProvider` | Which blocks an editor's value owns (by editor alias), so they are saved, deleted, versioned and copied with the page |
| `ISection`, `ISectionNavGroup`, `ISectionNav`, `ISectionNavGroupAction`, `ISectionDashboard` | Admin sections, nav groups, nav items, group "…" actions, dashboard tabs |
| `ITreeContextMenu` | Admin tree right-click menu items |
| `ICustomContentComponent`, `IDataListSource`, `ISeoCheck`, `IAccountLayout` | Components an editor picks for a page (given its `ContentId`, not the page), list-editor data sources, SEO checks, account-page layout |
| `IStorageProvider`, `IEmailProvider` | Media storage / email backend (selected by `Zauber:Plugins:*`) |
| `ICaptchaProvider` | CAPTCHA on the registration page (none shipped; `Zauber:Plugins:CaptchaProvider` when there are several) |
| `IStarterKit` | Starter content offered by the installer (the template ships one) |
| `IExternalAuthenticationProvider`, `IStartupPlugin` | OAuth providers, DI/startup hooks (parameterless ctor) |
| `ISeedData` | Runs every boot after migrations |
| `IValidate<T>`, FluentValidation `IValidator<T>` | Admin-side validation |
| `ITagReferenceProvider` | Which tags an editor's value holds (by editor alias), so the page save writes the tag links |
| `IBeforeEntitySave<T>`, `IAfterEntitySave<T>` | Save hooks, per row written (before can veto the whole change) |

`IContentProperty` and `ISectionNavGroupAction` take Radzen / modal types, so they live in ZauberCMS.Components under
their `ZauberCMS.Core.*` namespaces (D51); a new contract that needs a UI type goes there too, never in Core.
Pages are found by `EntryPage` (`ZauberCMS.Components/Pages`), and an address no page has goes to the content finders
(`IContentFinder`, D55, in `GetContentFromRequestAsync`); there is no MVC rendering path and no MVC views (removed in
Phase 11e, ZA-090). MVC is registered for controllers (`AddControllers` plus MVC's view features, no Razor view
engine), so a site's or plugin's controller can use the antiforgery attributes (D60).

## Configuration & cross-cutting

- Config section `"Zauber"` → `ZauberSettings` (`ZauberCMS.Core/Settings/ZauberSettings.cs`), read by
  `ZauberSettingsBinding` (a list the configuration sets replaces its default, ZA-167) and checked at start-up by
  `ZauberSettingsValidator` (D48): a setting that would fail at first use stops the start naming the setting; one
  that only does nothing as written is a start-up warning. A new setting must keep the defaults passing. Admin-editable runtime
  settings → `GlobalSettings`, stored as `GlobalData` alias `"GlobalSettings"`, read via `dataService.GetGlobalSettings()`.
- Cache: `ICacheService` keeps values in each server's memory (`ServerCache`); keys `TypeName-<hash of SQL + includes>`;
  invalidation by prefix (`StartsWith`). With `RedisConnectionString` set, Redis carries only the clears to the other
  servers (`ICacheInvalidationBus`); nothing cached goes to Redis. → [Docs/Services.md](Docs/Services.md) "Cache"
- Background jobs: the daily clean-up and the sitemap generator, each switched by `Zauber:Jobs:DailyCleanup` /
  `SitemapGenerator` (on by default; a web farm runs the clean-up on one server, D61). A new hosted job gets a
  switch there. → [Docs/Services.md](Docs/Services.md) "Background jobs"
- Events: singleton `AppState` with `WeakEventManager` (`OnXChanged/Saved/Deleted` for Content, ContentType, User,
  Media). Handlers fire on the publisher's thread in every circuit → marshal with `InvokeAsync`.
- Identity tables are `Zauber*`-prefixed; migrations history table `ZauberMigrations`. Keys are sequential GUIDs.
- Login-like pages use rate-limit policy `"login"` (per-IP 10 / 5 min); the registration page has its own,
  `"register"` (per-IP 5 POSTs / 10 min). Both from `Zauber:Identity:LoginRateLimit` / `RegisterRateLimit`. Identity lockout is active: six wrong passwords lock the account for
  five minutes.
- The first administrator comes from the installer, never from registering first. "Installed" is a `GlobalData`
  flag that is never unset; `Zauber:Identity:RecoverAdminUser` is the way back in for a site with no administrator.
  There is no switch for the installer: empty database settings always show it (D24).
  `GlobalSettings.AdminEmailAddresses` promotes an address only when its owner has proven it (a social login, or
  setting the password from an emailed link); an opened confirmation link is not enough. `GlobalSettings.AllowRegistration` turns
  public registration off (page 404, external first sign-in refused); when on, the page uses the replaceable
  `IFormProtectionService` (honeypot + fill time). → [Docs/Installer.md](Docs/Installer.md)
- Admin is gated by `[Authorize(Roles = Admin)]` in `ZauberCMS.Components/Admin/_Imports.razor` (that folder only).
  `AdminAccessMiddleware` (requests) and `Routes.razor` (navigation inside a circuit) treat any other page under
  `/admin` that has no `[Authorize]`/`[AllowAnonymous]` of its own as admin-only, and the middleware enforces
  `GlobalSettings.AllowedAdminIpAddress`. A plugin page for another role declares its own `[Authorize(Roles = …)]`.
- **Services do no authorisation** (Roadmap D27): the page, component or endpoint that calls a service checks
  access first. Services read the caller only to record who made a change, through `ICurrentUserAccessor` (circuit,
  static page, controller or job; `RunAs` for code with nobody signed in). Never inject `AuthenticationStateProvider`
  into a service. → [Docs/Services.md](Docs/Services.md) "Callers and access"
- Behind a reverse proxy set `Zauber:ForwardedHeaders`; set `Zauber:SiteUrl` on any site that sends email.
- The admin always runs in `Zauber:AdminDefaultLanguage` (`CultureMiddleware` for requests,
  `AdminCultureCircuitHandler` for circuits); front-end pages run in the language of their content.
- Rich text stores video embeds as placeholders; `IRichTextEmbedService` (replaceable) owns the provider list
  and expands them at render time, through the default `IHtmlSanitizerService`.

## High-risk areas — read the doc before editing

- **Block List editor & nested content** (`Editors/BlockList*`, `ContentEditScope`, `BlockPreview`, `ContentEditor.razor`
  block mode and save, `SaveContentAsync` (`ContentService.Save.cs` / `.Tree.cs`) / `NestedContentTree`, block snapshots). Block edits live in a
  `ContentEditScope` (child scope per side panel: commit on save, rollback on cancel) and the page is saved with its
  blocks in one transaction; Core finds blocks only through `INestedContentReferenceProvider`. Never save a block on its
  own from the editor, and don't revive the obsolete callback types. → [Docs/BlockListEditor.md](Docs/BlockListEditor.md)
- **Soft delete / recycle bin** — filter defaults on Get/Query parameters; flipping one breaks callers silently (ZA-001).
- **Bootstrap** (`ZauberSetup.cs`) — assembly order, middleware order, auto-migrate, seeders, and the DbContext
  that is chosen per scope so a site can start without a database.
- **Installer** (`ZauberCMS.Core/Installer/`) — runs with no database, no static files and nobody signed in, and
  is open to the first visitor until it finishes. → [Docs/Installer.md](Docs/Installer.md)
- **Migrations** — three providers, silent drift.

Open findings (`ZA-NNN`) and improvement proposals (`ZP-NNN`): [.audit/report.md](.audit/report.md); what was fixed
and how it was verified: [.audit/history.md](.audit/history.md).

## Definition of Done (every change)

1. `dotnet build ZauberCMS.sln -c Release` succeeds with **0 warnings** (warnings are errors, analysers included;
   [Docs/Standards.md](Docs/Standards.md) §10 says how to handle a rule that doesn't fit).
2. Schema touched → three migrations via `migrations.ps1`. Public API touched → no unannounced break (rule 2).
3. `dotnet test --solution ZauberCMS.sln -c Release` passes. Tests added/updated for changed behaviour: a bug fix
   comes with a regression test (or un-skips the one waiting for it); UI behaviour with no test yet → state how it
   was verified. → [Docs/Standards.md](Docs/Standards.md) §9. A csproj, packaging, the template or start-up touched →
   `./tests/template-smoke.ps1` passes too (a site made from the template, run through the installer; CI runs it).
4. **Docs updated in the same change** when behaviour, an extension point, a convention or a fact in this file changes.
   A doc that disagrees with code is a bug. Never write versions into docs — point at the csproj.
5. Self-review with the `doublecheck` skill; larger changes or before a release run the `audit` skill.

## Where to look

| Task | Read |
| --- | --- |
| Current rework: status, decisions, what to do next | [Docs/Roadmap.md](Docs/Roadmap.md) |
| Engineering standards, Blazor/.NET patterns, testing, security bar | [Docs/Standards.md](Docs/Standards.md) |
| Installer, first administrator, registration switch, CAPTCHA, starter kits | [Docs/Installer.md](Docs/Installer.md) |
| Services, parameter classes, caching, results, audit, events | [Docs/Services.md](Docs/Services.md) |
| Entities, mappings, migrations, provider gotchas | [Docs/Data.md](Docs/Data.md) |
| Front-end views, layouts, blocks, helpers, routing | [Docs/FrontEnd.md](Docs/FrontEnd.md) |
| Admin sections, nav, trees, context menus, dialogs, dashboards | [Docs/AdminUI.md](Docs/AdminUI.md) |
| Property editors and their settings | [Docs/Editors.md](Docs/Editors.md) |
| Block list editor (nesting, previews, persistence, versions) | [Docs/BlockListEditor.md](Docs/BlockListEditor.md) |
| CI, releases, NuGet packaging, the template | [Docs/Releasing.md](Docs/Releasing.md) |
| Full codebase audit (runbook) / open findings and proposals / fix history | [.claude/skills/audit/SKILL.md](.claude/skills/audit/SKILL.md) / [.audit/report.md](.audit/report.md) / [.audit/history.md](.audit/history.md) |

## Working agreements

- Default branch `main-static`; **every push to it publishes to nuget.org** (CI bumps the patch and commits the bump
  back). Commit/push only when asked; never push `master` (legacy). Pull before starting work.
- Tone: terse, expert reader, real code over theory. Never rename existing public members/types without asking.
- Keep context lean: read by line range, delegate broad searches, keep build output to errors/warnings.
