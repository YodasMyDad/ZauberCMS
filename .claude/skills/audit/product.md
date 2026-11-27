# Product review: editor UX (lens G) and Umbraco alignment (lens H)

The product agents P1–P4 ([areas.md](areas.md)) look at ZauberCMS as its users meet it: **editors** in the admin,
**visitors** on the site, and **site builders** who install it from NuGet and extend it. Each P agent walks its tasks
in a browser on a scratch site, reads the markup and code behind every screen it uses, and compares what it finds with
Umbraco. Write defects as findings (Category G or H) and improvements as proposals ([lenses.md](lenses.md), "Finding
or proposal?").

## Scratch site (the orchestrator starts it before the first product agent and stops it after the last)

Never run against the repository's `ZauberCMS.Web/app.db` or touch the dev site's media: the owner works in that tree.

1. `mkdir <run>/site/logs`. In **Bash** (PowerShell redirection corrupts binary files):
   `git show HEAD:ZauberCMS.Web/app.db > <run>/site/app.db`. That is the committed sample site (sample content, no
   accounts), not the owner's working copy.
2. List every file under `ZauberCMS.Web/wwwroot`, recursively, into `<run>/site/before.txt`: the app writes uploads
   (`media/`), resized images (`_mediacache/`, `is-cache/`) and sitemap XML there. Copy `ZauberCMS.Web/appsettings.json`
   to `<run>/site/appsettings.before.json`: it may hold the owner's uncommitted edits.
3. Start it in the background with these environment variables (environment beats every `appsettings*.json`):

   | Variable | Value |
   | --- | --- |
   | `ASPNETCORE_ENVIRONMENT` | `Development` |
   | `Zauber__DatabaseProvider` | `Sqlite` |
   | `Zauber__ConnectionString` | `Data Source=<run>/site/app.db;Cache=Shared` |
   | `Serilog__WriteTo__0__Args__path` | `<run>/site/logs/log.txt` (the File sink is the first `WriteTo` entry in `ZauberCMS.Web/appsettings.json`; check) |
   | `Serilog__MinimumLevel__Override__Microsoft.EntityFrameworkCore.Database.Command` | `Information` (only while measuring the hot-path budget; it is noisy) |

   Command: `dotnet run --project ZauberCMS.Web -c Release --no-launch-profile --urls http://127.0.0.1:5399`.
   Release keeps clear of a Debug build the owner may be running; the port keeps clear of theirs (5177). Wait for
   "Now listening on".
4. Opening `http://127.0.0.1:5399/` shows the installer, because the sample database has no accounts. P1 completes
   it (task I1) and writes the administrator's email and a generated password to `<run>/site/credentials.txt`.
5. Stopping: end the background process. Delete the files the run added under `ZauberCMS.Web/wwwroot` (compare with
   `before.txt`), and nothing else. If `ZauberCMS.Web/appsettings.json` differs from `appsettings.before.json`, copy
   the saved one back. `git status --short` must show the same as before the run. Never `git checkout` or `git clean`
   to tidy up: other changes in the tree belong to the owner.

Only SQLite is walked. Say so in coverage: PostgreSQL, SQL Server and Redis behaviour comes from the tests and the code.

## Walkthrough

Use the browser tools (Playwright: snapshots to read the page, screenshots for layout, console messages, network).
For every task, log in `<run>/<agent-id>.md`: the steps it took (screens and clicks), what happened, every point of
friction against the G checklist, defects, and how Umbraco does the same task. After each group of tasks, read the
browser console and `<run>/site/logs/log.txt`, and report every error and warning they show. Time anything that takes
more than a second to appear.

On top of the tasks, once in each P agent's area:

- do one task with the keyboard only (tab order, visible focus, Enter and Escape, menus and trees by keyboard);
- view each screen at 1280×720 and at 768 px wide;
- reload in the middle of an edit, and go offline for 10 s in the middle of one (Playwright can set the context
  offline): what happens to the unsaved work, and what does the editor see?
- try every destructive action and read its confirmation: does it name the item and say what else goes with it?

### P1: Installer and content

- **I1** Install: read every step as a first-time owner would; try a wrong value at each step; finish.
- **C1** Create a page under Home with each document type the site offers; fill every editor; Save, then publish; open it on the site.
- **C2** Change a published page's title and leave without saving (expect a warning); come back; save a draft only and check the site still shows the published version.
- **C3** Block list: add, reorder, edit and remove blocks two levels deep; cancel a nested panel (its changes must go); save.
- **C4** Pickers: pick content and media, single and multiple; reorder; remove one; save; check the front end.
- **C5** Rich text: format, link to a page, insert an image, embed a video, paste from a word processor.
- **C6** Copy a page with children, move a page (also try under its own child), sort children.
- **C7** Versions: compare two versions, restore an older one, check the site.
- **C8** Delete a page with children to the bin; restore it; delete one permanently; empty the bin.
- **C9** Restrict access to a page; set culture and domains.
- **C10** In a node with many children, find one page by name (tree, list view, search if any).
- **C11** Two tabs on the same page: save in one, then save in the other.
- **C12** Find all unpublished changes across the site.

### P2: Media

- **M1** Upload several files by drag and drop and by the file picker; one disallowed type; one too large.
- **M2** Create a folder, move files into it, sort, rename.
- **M3** Edit an image (crop or focal point, if offered); set alt text; check how it renders on the site.
- **M4** Delete an image a page uses (expect a usage warning); restore; delete permanently.
- **M5** Find one image in a folder of 100+ items, and pick it from a content page's media picker.

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

- **A1** Create a user, give roles, change the password, lock and unlock (if offered), delete. Create a role.
- **A2** Sign out; sign in with a wrong password twice; use forgot-password; open the registration page (whatever its setting).
- **A3** What can a non-admin role see and do in the admin? (D27: back-office roles are Backlog; note the gap.)
- **A4** Restricted page from C9 as an anonymous visitor and as a signed-in member.
- **F1** As a visitor: a 404, an error page, the restricted page, a page in a second language.
- **F2** Page source: title, meta, canonical, hreflang, Open Graph, sitemap, robots.
- **F3** Response headers and caching for a page, an image, a resized image and a fingerprinted script.
- **F4** Page weight and speed: HTML size, requests, render time for a page with blocks.
- **D1** From `ReadMe.md` and the template only: create a site and get to a running page (reuse the template smoke test from SKILL.md §2 if it ran; judge the instructions, not only the build).
- **D2** From `Docs/FrontEnd.md` and `Docs/Editors.md` only: work out what it takes to add a view for a new document type and a new property editor. No code is committed; note every gap in the docs.
- **D3** From `Docs/AdminUI.md`: what it takes to add an admin section with a tree, a dashboard and a context-menu action.
- **D4** From `Docs/Services.md`: what it takes to react to a page being published (a save hook), and to replace the storage or email provider.

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
8. **Scale**: a tree of 1,000 pages, a folder of 10,000 media items, 100 users — paging, search and virtualisation hold up.
9. **Accessibility** (WCAG 2.2 AA basics; defects go into the roll-up, lenses.md E).
10. **Resilience**: a lost connection, a reconnect, an error in one editor (D32), a slow server — the editor keeps
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

For each row of the map below, compare concept, name, workflow and defaults, and classify:

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

| ZauberCMS | Umbraco | Compare |
| --- | --- | --- |
| Content section: tree, `ContentEditor`, `ContentListView` | Content section, document workspace, collection (list view) | Save / Save and publish / Unpublish wording and order; publish with descendants; the Info view (links, history, URLs, template); list columns, filters, bulk actions |
| Document, element and composition types (`ContentTypeEditor`, `CompositionsTree`, `ElementTypesTree`) | Document Types, Element Types, Compositions | allowed children and allowed at root; tabs and groups; icons; variation; templates; history clean-up per type |
| Property editor + per-property settings (`IContentProperty`, `IContentPropertySettings<T>`, `ContentTypePropertySettingsEditor`) | Data Types: an editor plus its configuration, reusable across types | configuring an editor once and reusing it; changing a configuration in one place |
| Block List (`BlockListEditorProperty`) | Block List and Block Grid | content and settings models per block; labels; min and max; copy and paste of blocks (clipboard); inline editing; a grid layout |
| Versions and `VersionComparison` | Rollback with a diff; content version clean-up | keep-N (D25 vs Umbraco's clean-up policy) |
| Content and media recycle bins | Recycle bin | restoring to the original place or choosing a parent; empty-bin confirmation |
| `RestrictAccess` (`ContentRole`) | Public Access (member groups or one member; login and error pages chosen per rule) | |
| `CultureDomains`, `Languages` | Culture and Hostnames, Languages, variants | Umbraco varies by culture per property inside one document; record how ZauberCMS models languages and what an editor must do differently |
| `LanguageDictionaries` | Translation section: Dictionary | |
| Media section, `MultipleMediaUpload`, `EditImage` | Media section, Image Cropper (crops and focal point on the data type), Media Picker | crops and focal point; alt text; drag and drop into folders; a list view for media |
| Users and roles (`UserEditor`, `RoleEditor`) | Users and User Groups (allowed sections, start nodes, granular permissions); separately, Members and Member Groups | back-office users vs site members; start nodes; permissions per node (D27 Backlog) |
| `AuditLog` | History in each node's Info view; the Log Viewer | history where the editor works, not only in Settings |
| `SeoRedirects`, `SeoSitemaps`, `SeoChecker` | Redirect URL Management (301s created automatically when a published URL changes); sitemaps and SEO via packages | redirects created on rename or move |
| `ISectionDashboard`, `ISection`, `ISectionNav`, `ITreeContextMenu`, `ISectionNavGroupAction` | Dashboards, sections, trees, entity actions (back-office extensions) | |
| `IStartupPlugin` | Composers (`IComposer`) | |
| `IBeforeEntitySave<T>` / `IAfterEntitySave<T>` | Notifications (`ContentSavingNotification`… with `INotificationHandler<T>`; "-ing" ones can cancel) | per row (D28) vs per operation; after the commit (ZA-163) |
| `IContentView` + layouts | Templates (Razor views) and route hijacking (`RenderController`) | choosing a view per type and per page |
| `GetValue<T>` and the value-conversion extensions | Property value converters (`IPropertyValueConverter`), Models Builder | typed models; a way for an editor's author to define its front-end type |
| `IContentFinder` (unwired) | Content finders, `IContentLastChanceFinder` (404), URL providers | |
| `IStorageProvider` | Media file system providers (Azure Blob) | |
| `IEmailProvider` | `IEmailSender`, SMTP settings | |
| `IExternalAuthenticationProvider` | External login providers (back office and members) | |
| `ISeedData` | Migration plans, package migrations | runs on every boot vs once per version |
| `IStarterKit` + installer | Installer and starter kits | |
| Cache + Redis invalidation | Published cache per server; distributed cache refreshers; load balancing | which server runs scheduled jobs: Umbraco runs them on one server only; `DailyJob`? |
| `GlobalSettings` (edited in the admin, stored in the database) | `Umbraco:CMS:*` in `appsettings.json` | what belongs in the admin and what in configuration |
| `DailyJob` (version and audit retention) | Scheduled jobs (content version clean-up, log clean-up) | |

**Umbraco features with no ZauberCMS counterpart found by file name** (2026-11-27). Check each in code and in the
admin before proposing; some may exist under another name: scheduled publish and unpublish; preview; document
blueprints (content templates); search across the back office; a search index (Examine); health checks; webhooks;
Content Delivery API (headless); Models Builder; two-factor sign-in for users; separate members; user start nodes;
notifications (subscribe to changes on a node); an Image Cropper; a multi-URL picker (`NavigationProperty` may cover
it); relations (media usage exists: `MediaUsageText`); publish with descendants; per-property language variants;
copy and paste of blocks.

### Different on purpose (do not propose; add a row when the owner declines a proposal)

| Difference | Source |
| --- | --- |
| Extensions are C#/Razor types discovered by reflection; no back-office JS manifests and no front-end build chain for site builders | product goal (ReadMe, AGENTS.md) |
| The admin is Blazor Server with Radzen, not Umbraco's web components: alignment is about concepts, words and workflows, not the look | product goal |
| No unattended install; the installer is open to the first visitor; the installed flag is never unset; no switch to turn it off | D19, D20, D21, D24 |
| Services do no authorisation; the page or endpoint checks access (as Umbraco does) | D27 |
| Entity graphs cached in each server's memory; Redis carries only invalidation | D13 |
| Save hooks run per row written, one veto stops the whole change; a draft runs the `UnpublishedContent` hooks | D28, D29 |
