# Product review and runtime probes: the scratch site, lens G (UX), lens H (Umbraco)

The product agents (P1a, P1b, P2, P3, P4; [areas.md](areas.md)) look at ZauberCMS as its users meet it: **editors** in
the admin, **visitors** on the site, and **site builders** who install it from NuGet and extend it. Each one walks its
tasks in a browser on a scratch site, reads the markup and code behind every screen it uses, and compares what it
finds with Umbraco. The runtime probes (R1, R2) measure what reading can't: queries per request, memory, a cold-cache
burst, an upgrade, two servers. Write defects as findings (Category G or H, or the lens a probe measures) and
improvements as proposals ([lenses.md](lenses.md), "Finding or proposal?").

## Scratch site (the orchestrator prepares it after K's build and stops it after verification)

The site runs as a **published copy in Production**, so it behaves like a live site (no developer exception page, no
detailed errors, HSTS, the real error page) and nothing it writes lands in the repo. Running `ZauberCMS.Web` with
`dotnet run` in Development would load the owner's untracked `appSettings.Development.json` (real SMTP settings),
write uploads into their dev site's `wwwroot`, and lock `bin/Release`.

1. **Publish** from `<root>`: `dotnet publish <root>/ZauberCMS.Web -c Release -o <run>/site/app`.
2. **Database**: the committed sample site, not the owner's working copy. In Bash (PowerShell redirection corrupts
   binary files): `git show HEAD:ZauberCMS.Web/app.db > <run>/site/app.db`. It has sample content and no accounts.
3. **Seed** (before the first start). With no accounts, `InstallerMiddleware` sends every request to the installer.
   Seed through the services, never raw SQL, with a throwaway harness in a worktree (`<run>/src`, or `<run>/repro`
   when the target is `+wt`). It is never committed. `TestSite` in `tests/ZauberCMS.Core.Tests/Infrastructure/` shows
   how to build the real DI graph without a web host; point it at `<run>/site/app.db`. Seed:
   - an administrator, with its email and a generated password written to `<run>/site/credentials.txt` (a site with
     a user counts as installed, D21);
   - scale data under a page named "Scale test": about 1,000 pages over three levels, 2,000 media items in one folder
     (one small image reused), 100 users.

   If seeding doesn't work within a reasonable effort, finish the installer's account step in the browser instead,
   and record the scale checks (G8, B1, B4) as not measured.
4. **Start** in the background. Pass the settings as command-line arguments: they beat `appsettings*.json` and
   environment variables, and dotted keys work there (they don't as variables). Quote each argument in PowerShell:

   ```text
   dotnet <run>/site/app/ZauberCMS.Web.dll --contentRoot <run>/site/app --environment Production
     --urls http://127.0.0.1:5399
     --Zauber:DatabaseProvider=Sqlite "--Zauber:ConnectionString=Data Source=<run>/site/app.db;Cache=Shared"
     --Zauber:SiteUrl=http://127.0.0.1:5399
     --Zauber:Email:Smtp:Host=127.0.0.1 --Zauber:Email:Smtp:Port=9
     "--Serilog:WriteTo:0:Args:path=<run>/site/logs/log.txt"
     "--Serilog:WriteTo:0:Args:outputTemplate={Timestamp:HH:mm:ss.fff} [{Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}"
   ```

   SMTP points at a closed port, so no email can leave; a send fails and is logged. Check that `WriteTo:0` is the
   File sink in the published `appsettings.json`. The file rolls daily, so read `logs/log*.txt`. Plain http works:
   `UseHttpsRedirection` has no https port to send to. If it does redirect, bind `https://127.0.0.1:5399` with the
   development certificate. Port 5399 keeps clear of the owner's dev site (5177).
5. **Wait** by polling `http://127.0.0.1:5399/` until it answers. Serilog's Warning level hides "Now listening on".
6. **Restarting** (R1 does it): stop the process and start it again with the extra arguments.
7. **Stopping**: end the process. Everything it wrote is in `<run>`, so there is nothing to clean in the repo.
   `git status --short` must show the same as before the run.

Only SQLite on one server is walked. PostgreSQL, SQL Server and Redis come from the tests, the code and R2; say so in
coverage.

## Walkthrough

Use the browser tools (Playwright: snapshots to read a page, screenshots for layout, console messages, network).
**Write your notes after each task, before starting the next**: Radzen pages make large snapshots, and a full context
loses what was not written down. For every task log the steps (screens and clicks), what happened, each point of
friction against the G checklist, any defects, and how Umbraco does the same task. After each group of tasks, read
the browser console and `<run>/site/logs/log*.txt`, and report every error and warning in them. Time anything that
takes more than a second to appear.

On top of the tasks, once in each product agent's area:

- do one task with the keyboard only (tab order, visible focus, Enter and Escape, menus and trees by keyboard);
- view each screen at 1280×720 and at 768 px wide;
- reload in the middle of an edit, and go offline for 10 s in the middle of one (Playwright can set the context
  offline): what happens to the unsaved work, and what does the editor see?
- try every destructive action and read its confirmation: does it name the item and say what else goes with it?

### P1a: Installer and content creation

- **I1** Installer, on the template site K built (`<run>/tmpl/AuditSite`): start it the same way as the scratch site
  but from its own folder, on port 5400, in Production, with no database settings and the SMTP override. Read every
  step as a first-time owner would, including the database step and the template's starter kit; try a wrong value at
  each step; finish; stop it. Nothing else uses that site.
- **C1** Create a page under Home with each document type the site offers; fill every editor; Save, then publish; open it on the site.
- **C2** Change a published page's title and leave without saving (expect a warning); come back; save a draft only and check the site still shows the published version.
- **C3** Block list: add, reorder, edit and remove blocks two levels deep; cancel a nested panel (its changes must go); save.
- **C4** Pickers: pick content and media, single and multiple; reorder; remove one; save; check the front end.
- **C5** Rich text: format, link to a page, insert an image, embed a video, paste from a word processor.
- **C6** Copy a page with children, move a page (also try under its own child), sort children.

### P1b: Content history and control

- **C7** Versions: compare two versions, restore an older one, check the site.
- **C8** Delete a page with children to the bin; restore it; delete one permanently; empty the bin.
- **C9** Restrict access to a page; set culture and domains.
- **C10** Under "Scale test", find one page by name (tree, list view, search if any); note how the tree and list behave at that size.
- **C11** Two tabs on the same page: save in one, then save in the other.
- **C12** Find all unpublished changes across the site.

### P2: Media

- **M1** Upload several files by drag and drop and by the file picker; one disallowed type; one too large.
- **M2** Create a folder, move files into it, sort, rename.
- **M3** Edit an image (crop or focal point, if offered); set alt text; check how it renders on the site.
- **M4** Delete an image a page uses (expect a usage warning); restore; delete permanently.
- **M5** Find one image in the 2,000-item folder, and pick it from a content page's media picker.

### P3: Structure and settings

- **S1** Create an element type, and a document type with tabs, a composition and properties using several editors with settings.
- **S2** Allow the new type where pages can be created; create a page from it.
- **S3** Change a property alias on a type that has content; reorder properties; move a property to another tab.
- **S4** Copy and move a type; delete a type that is in use (expect a guard).
- **X1** Global settings: change one setting, save, see its effect.
- **X2** Languages: add a language; add and translate dictionary items; see them on the site.
- **X3** SEO: add a redirect (and an invalid one); create a sitemap; run the SEO checker on a page.
- **X4** Audit log: find who changed the page in C2, and when.
- **X5** Dashboards: what each section's dashboard tells a new editor, and whether it helps.

### P4: Users, the public site, building on ZauberCMS

- **A1** Create a user, give roles, change the password, lock and unlock (if offered), delete. Create a role. Find one of the 100 seeded users.
- **A2** Sign out; sign in with a wrong password twice; use forgot-password (the email fails on purpose: what does the page say?); open the registration page (whatever its setting).
- **A3** What can a non-admin role see and do in the admin? (D27: back-office roles are in the Backlog; note the gap.)
- **A4** The restricted page from C9, as an anonymous visitor and as a signed-in member.
- **F1** As a visitor: a 404, an error page, the restricted page, a page in a second language.
- **F2** Page source: title, meta, canonical, hreflang, Open Graph, sitemap, robots.
- **F3** Response headers and caching for a page, an image, a resized image and a fingerprinted script.
- **F4** Page weight: HTML size, number of requests, time to render a page with blocks (the query count is R1's).
- **D1** From `ReadMe.md` and the template only: how a new site builder gets to a running page (use K's template results; judge the instructions, not only the build).
- **D2** From `Docs/FrontEnd.md` and `Docs/Editors.md` only: what it takes to add a view for a new document type and a new property editor. No code is committed; note every gap in the docs.
- **D3** From `Docs/AdminUI.md`: what it takes to add an admin section with a tree, a dashboard and a context-menu action.
- **D4** From `Docs/Services.md`: what it takes to react to a page being published (a save hook), and to replace the storage or email provider.

## Runtime probes

### R1 (after P4; the site and the browser are R1's alone)

- **Hot-path budget.** Restart the site with
  `--Serilog:MinimumLevel:Override:Microsoft.EntityFrameworkCore.Database.Command=Information` and
  `--Serilog:MinimumLevel:Override:Serilog.AspNetCore.RequestLoggingMiddleware=Information`. Request these one at a
  time with a short pause, three times each, and count the `Executed DbCommand` lines between one request-log line
  and the next. Keep the lowest count: a page cold (the first request after a start), the same page warm, the same
  page after the front-end cache has expired (Docs/Services.md "Cache"), a 404, a page with a block list, a page with
  pickers, a restricted page. Report each number next to the last report's. Restart without the overrides afterwards.
- **Memory.** Install the tools into the run folder, never globally: `dotnet tool install dotnet-gcdump --tool-path
  <run>/tools` (the same for `dotnet-counters`). Take a gcdump after a warm-up. Then make about 50 admin navigations
  in one tab (open pages and side panels, switch sections), take another, open and close 5 admin tabs, wait past
  the disconnected-circuit retention (`CircuitOptions.DisconnectedCircuitRetentionPeriod`, 3 minutes by default), and
  take a third. `dotnet-gcdump report` each one. The heap should come back near the first. List the types that grew
  (components, `Content` and other entities, DbContexts, strings, byte arrays). Watch `System.Runtime` (and the
  Blazor circuit counters, if this .NET publishes them) with `dotnet-counters` while doing it.
- **Cold-cache burst.** Right after a restart, send 20 requests at once for one page, then 20 spread over 5 pages
  (Bash: `curl … &` in a loop, then `wait`). Count the database commands (a stampede shows as 20× the cold count),
  and look for errors, 500s and "database is locked".
- **Upgrade** (`full`). Stop the site. If the tag of the last release exists (`git tag`), copy that release's
  database: `git show <tag>:ZauberCMS.Web/app.db > <run>/site/upgrade.db`. Start the published site on it (port 5401).
  The migrations must apply, the site counts as installed if that database has users (D21), pages must render (the
  stored view and editor type names still resolve), and the admin must open. Then stop it and start the normal site
  again for the verifiers.

### R2 (`full`, when Docker is available: two servers)

Start PostgreSQL and Redis containers on unused ports, named `zauber-audit-…`, and remove them at the end. Run two
copies of the published site (ports 5402 and 5403) on the same database and Redis (`--Zauber:DatabaseProvider` takes
the PostgreSQL name in `DatabaseProviders`; also pass `--Zauber:RedisConnectionString`). Install through the
installer on one of them. Then check lens A5: a save on one server shows on the other's front end (the cache clear
over Redis); what the other server's admin tree and open editor show; rate limits counted per server; `DailyJob` and
sitemap generation on both; a file uploaded on one served by the other. If R2 can't run, list it under "Out of reach"
in coverage with the reason.

## G. Editor and admin UX checklist

1. **Finding your way**: sections and trees make sense to someone who has used Umbraco; the tree keeps its expanded
   state and highlights and scrolls to the current item; breadcrumbs; deep links survive a reload; Back works.
2. **Feedback**: every action confirms success or shows a specific error next to the cause; buttons show they are
   busy and cannot be pressed twice; nothing fails silently; messages say what to do next.
3. **Safety**: unsaved changes are guarded on navigation, panel close and reload; destructive actions name the item
   and what goes with it (children, usages) and say whether they can be undone; there is a way back (bin, versions).
4. **Efficiency**: count the steps of each common task and compare with Umbraco; Ctrl+S to save; bulk actions in
   lists; search and filters on long lists and in pickers; sensible defaults; choices remembered.
5. **Consistency**: the same word for the same action everywhere (Save, Publish, Delete, Remove, Recycle); buttons in
   the same place; side panels vs dialogs used the same way; icons, dates and empty states the same.
6. **Forms**: labels; descriptions and help text from the document type; required markers; validation inline and on
   save, with focus moved to the first error, and the same rules on the server; counters for length-limited fields.
7. **Preview**: an editor can see the page before publishing; block previews are readable and match the site.
8. **Scale**: the "Scale test" data (1,000 pages, 2,000 media, 100 users): paging, search and virtualisation hold up.
9. **Accessibility** (WCAG 2.2 AA basics; defects go into the roll-up, lenses.md E).
10. **Resilience**: a lost connection, a reconnect, an error in one editor (D32), a slow server: the editor keeps
    their work and knows what happened.
11. **Layout**: laptop and tablet widths; stacked side panels stay usable; long names and long values don't break the layout.
12. **Words**: plain English for editors; developer terms (alias, element type, composition) explained where they appear.
13. **First use**: an empty site and each dashboard guide a new owner to the next step; the installer explains each choice.
14. **Site builders**: misconfiguration explains itself (`MissingView`, a missing layout, an editor with bad settings);
    docs match the code; extension points are easy to find.

## H. Umbraco alignment

ZauberCMS follows Umbraco on purpose, scaled down: an Umbraco user should recognise the concepts, the words and the
workflows. When the owner delegated design questions (see the D-decisions), the rule was "an enterprise CMS, compared
with how Umbraco does it".

**Reference**: the current Umbraco LTS at the time of the run, with its new back office (v14 and later), not v13 or
earlier from memory. Name the version in the report header. Confirm each claim in the current docs
(docs.umbraco.com) and link the page in the proposal. Web searches carry only generic Umbraco questions, never
anything about this project.

Each row of the map below belongs to the product agent in its P column. That agent compares concept, name, workflow
and defaults, classifies the row, and writes it into its `.md` file under "Umbraco alignment" (M builds the report's
table from those):

| Result | What to write |
| --- | --- |
| Aligned | nothing; a word in the alignment table |
| Same idea, different for no reason (a name, a word, a button order, a default, a missing step) | a proposal to align, usually cheap |
| Missing: Umbraco has it, editors or site builders rely on it | a proposal with value and effort; propose the smallest version that covers the common need |
| Different on purpose (table below, or a D-decision) | nothing; don't re-propose it |
| Better here | a word in the alignment table; keep it |

Don't propose Umbraco's complexity wholesale: the product goal is simple and modular, extended in plain C#/Razor, with
no front-end build chain for site builders. A parity feature ships as a replaceable default or an extension point
wherever Umbraco offers one (AGENTS.md, core principle).

### Concept map (starting points: confirm both sides before citing)

| P | ZauberCMS | Umbraco | Compare |
| --- | --- | --- | --- |
| P1a | Content section: tree, `ContentEditor`, `ContentListView` | Content section, document workspace, collection (list view) | Save / Save and publish / Unpublish wording and order; publish with descendants; the Info view (links, history, URLs, template); list columns, filters, bulk actions |
| P1a | Block List (`BlockListEditorProperty`) | Block List and Block Grid | content and settings models per block; labels; min and max; copying and pasting blocks; inline editing; a grid layout |
| P1b | Versions and `VersionComparison` | Rollback with a diff; content version clean-up | keep-N (D25) against Umbraco's clean-up policy |
| P1b | Content recycle bin | Recycle bin | restoring to the original place or choosing a parent; empty-bin confirmation |
| P1b | `RestrictAccess` (`ContentRole`) | Public Access (member groups or one member; login and error pages chosen per rule) | |
| P1b | `CultureDomains`, `Languages` | Culture and Hostnames, Languages, variants | Umbraco varies by culture per property inside one document; record how ZauberCMS models languages and what an editor must do differently |
| P1a | Installer and `IStarterKit` | Installer; starter kits are NuGet packages added to the project, not an installer step | |
| P2 | Media section, `MultipleMediaUpload`, `EditImage`, media bin | Media section, Image Cropper (crops and focal point on the data type), Media Picker | crops and focal point; alt text; drag and drop into folders; a list view for media |
| P3 | Document, element and composition types (`ContentTypeEditor`, `CompositionsTree`, `ElementTypesTree`) | Document Types, Element Types, Compositions | allowed children and allowed at root; tabs and groups; icons; variation; templates; history clean-up per type |
| P3 | Property editor + per-property settings (`IContentProperty`, `IContentPropertySettings<T>`, `ContentTypePropertySettingsEditor`) | Data Types: an editor plus its configuration, reusable across types | configuring an editor once and reusing it; changing a configuration in one place |
| P3 | `LanguageDictionaries` | Translation section: Dictionary | |
| P3 | `AuditLog` | History in each node's Info view; the Log Viewer | history where the editor works, not only in Settings |
| P3 | `SeoRedirects`, `SeoSitemaps`, `SeoChecker` | Redirect URL Management (301s created automatically when a published URL changes); sitemaps and SEO via packages | redirects created on rename or move |
| P3 | `ISectionDashboard` dashboards | Dashboards per section | |
| P3 | `GlobalSettings` (edited in the admin, stored in the database) | `Umbraco:CMS:*` in `appsettings.json` | what belongs in the admin and what in configuration |
| P4 | Users and roles (`UserEditor`, `RoleEditor`) | Users and User Groups (allowed sections, start nodes, granular permissions); separately, Members and Member Groups | back-office users vs site members; start nodes; permissions per node (D27 Backlog) |
| P4 | Account pages, `IAccountLayout`, `IExternalAuthenticationProvider` | Member login (Razor templates), external login providers for the back office and members | |
| P4 | `ISection`, `ISectionNav`, `ITreeContextMenu`, `ISectionNavGroupAction` | Sections, trees, entity actions (back-office extensions) | |
| P4 | `IStartupPlugin` | Composers (`IComposer`) | |
| P4 | `IBeforeEntitySave<T>` / `IAfterEntitySave<T>` / `IAfterEntityCommit<T>` | Notifications (`ContentSavingNotification`… with `INotificationHandler<T>`; the "-ing" ones can cancel, the "-ed" ones are raised when the scope completes) | per row (D28) vs per operation; after-commit hook (D62) |
| P4 | `IContentView` + layouts | Templates (Razor views) and route hijacking (`RenderController`) | choosing a view per type and per page |
| P4 | `GetValue<T>` and the value-conversion extensions | Property value converters (`IPropertyValueConverter`), Models Builder | typed models; a way for an editor's author to define its front-end type |
| P4 | `EntryPage` + `GetContentFromRequestAsync` (no finder pipeline; the unwired `IContentFinder` went in Phase 11e) | Content finders, `IContentLastChanceFinder` (404), URL providers | a site cannot add its own way of finding the page for an address |
| P4 | `IStorageProvider`, `IEmailProvider` | Media file system providers (Azure Blob), `IEmailSender` | |
| P4 | `ISeedData` | Migration plans, package migrations | runs on every boot vs once per version |
| R2 | Per-server memory cache + Redis invalidation (D13) | HybridCache since v15: in-memory per server, with an optional shared distributed layer; load-balanced back office | what an editor on server B sees after a save on A |
| R2 | `DailyJob` (version and audit retention) | Scheduled jobs; once-only jobs as distributed background jobs tracked in the database | two servers running the same job |

**Umbraco features with no ZauberCMS counterpart found by file name** (2026-11-27). Check each in code and in the admin
before proposing; some may exist under another name. Owner in brackets: scheduled publish and unpublish (P1a);
preview (P1a); document blueprints, i.e. content templates (P1a); publish with descendants (P1a); copy and paste of
blocks (P1a); per-property language variants (P1b); search across the back office (P1b); notifications on changes to a
node (P1b); an Image Cropper (P2); relations, beyond the media usage that `MediaUsageText` shows (P2); a multi-URL
picker (P3; `NavigationProperty` may cover it); health checks (P3); webhooks (P3); a search index like Examine (P4);
the Content Delivery API, i.e. headless (P4); Models Builder (P4); two-factor sign-in for users (P4); separate members
(P4); user start nodes (P4).

### Different on purpose (do not propose; add a row when the owner declines a proposal)

| Difference | Source |
| --- | --- |
| Extensions are C#/Razor types discovered by reflection; no back-office JS manifests and no front-end build chain for site builders | product goal (ReadMe, AGENTS.md) |
| The admin is Blazor Server with Radzen, not Umbraco's web components: alignment is about concepts, words and workflows, not the look | product goal |
| No unattended install; the installer is open to the first visitor; the installed flag is never unset; no switch to turn it off | D19, D20, D21, D24 |
| Services do no authorisation; the page or endpoint checks access (as Umbraco does) | D27 |
| Entity graphs cached in each server's memory; Redis carries only invalidation | D13 |
| Save hooks run per row written, and one veto stops the whole change; a draft runs the `UnpublishedContent` hooks | D28, D29 |
