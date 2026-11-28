# Admin UI

`/admin` is `InteractiveServer` + Radzen + Blazored.Modal. Code lives in `ZauberCMS.Components/` (paths below are
relative to it unless prefixed). Everything is discovered by interface (see AGENTS.md → Extension points); public
non-abstract types only, keyed by `Type.FullName`. Metadata properties (names, aliases, sort orders) are read from
instances created by `ActivatorUtilities` — `[Inject]` is **not** populated there, so keep metadata as plain values.
Radzen, the modal and the contracts that take their types (`IContentProperty`, `ISectionNavGroupAction`, the
notification, dialog and tree extensions, `ValidateService<T>`, `MediaValidationService`) are in this assembly under
their `ZauberCMS.Core.*` / `Blazored.Modal*` namespaces (`Core/`, `Modal/`; Roadmap D51); their services are
registered by `Startup/AdminUiStartup.cs`. A plugin with admin UI references `ZauberCMS.Components`.

## Page skeleton

```razor
@page "/admin/myfeature"
@layout SectionLayout
@attribute [AuthorizeSection(MySection.SectionAlias)]   @* ZauberCMS.Core.Sections.Authorization; automatic inside each Components/Admin/<Section>/ folder *@

@code {
    [CascadingParameter] public SectionLayout? Layout { get; set; }
    protected override void OnInitialized() => Layout?.SetSection(MySection.SectionAlias); // also sets TreeState.CurrentSection
}
```

- Route under `/admin` — that is what gives InteractiveServer and loads Radzen/admin assets (`ZauberCMS/Components/App.razor`).
  The admin is **not prerendered** (ZA-101): a page initialises once, in the circuit, and `HttpContext` is null in
  it. An admin page is always loaded as a full page: `AdminAccessMiddleware` drops Blazor's `blazor-enhanced-nav`
  response header, so a link from a static page (a site's view, an account page) does not patch the admin into that
  page with its scripts not run (ZA-096). A link of your own into `/admin` can add `data-enhance-nav="false"` to skip
  the browser's fallback warning.
- Admin scripts are served with fingerprinted names (`@Assets[...]`): Radzen, `block-preview.js`, Sortable.js
  (shipped in the Components package under `wwwroot/lib/sortablejs`, MIT; no CDN), and each editor's `Scripts` and
  `Styles` (a path that is not a static web asset, such as a CDN address, is used as it is).
- `Admin/Layout/SectionLayout.razor`: top nav (`SectionLinks`), sidebar trees (`SectionTrees`), the cascading modal,
  and the single `<RadzenComponents />` host (a second one doubles every dialog and notification).
- The sidebar's width can be dragged (owner request, Phase 10d): the divider on its edge
  (`.zauber-sidebar-resizer`, `wwwroot/js/admin-sidebar.js`) sets Radzen's `--rz-sidebar-width` on `<html>`, between
  200px and 60% of the window (at most 720px), and keeps it per browser in `localStorage`
  (`zauber.admin.sidebarWidth`). Keyboard: focus the divider, arrow keys (Shift for bigger steps), Home, End; Enter or
  a double-click goes back to the theme's 300px. Hidden on narrow screens and when the sidebar is collapsed. The
  trees scroll inside `.zauber-sidebar-scroll`.
- Access: per section and role, see "Access" below. The admin IP allow-list (`GlobalSettings.AllowedAdminIpAddress`)
  is enforced by `AdminAccessMiddleware` for every `/admin` request: 403.
  The layout is cascaded with `IsFixed="true"`: use it to call `SetSection`, don't read changing state from it.
- An editor page loads its model when its route values change, not on every `OnParametersSetAsync` — parameters
  are set again whenever a parent re-renders, and reloading there drops unsaved edits (see `ContentTypeEditor`).
- Context-menu actions (`ITreeContextMenu`, `ISectionNavGroupAction`) are run by `BaseTree` / `SectionTrees`, which
  log an exception and show a generic error. Still report expected failures yourself (`HandlerResult.Messages`).
- Routes are found because `Routes.razor` passes `ExtensionManager.GetFilteredAssemblies(null)` as additional assemblies.
- Admin URLs live in `ZauberCMS.Core/Urls.cs` (e.g. `Urls.AdminUpdateContent`). Plugins use their own constants.

## Access: sections and roles (Roadmap D38, D66–D71)

Umbraco's user groups, first step: which roles may open each section. Start nodes and per-action permissions
(publish, delete, move) are not part of it; a user who may open a section can do everything its pages offer.

- **Stored on the role.** `Role.ExtendedData["AllowedSections"]`, a list of `ISection.Alias`es, read and written with
  `RoleExtensions.GetAllowedSections()` / `SetAllowedSections()`, saved with the role (`SaveRoleAsync`). No table or
  column. A user opens the sections of all their roles together; `Admin` opens every section, plugins' included,
  without a list. Set on the role's page in Users (the "Sections" box lists every discovered section). The role
  every registration gets (`Zauber:NewUserStartingRole`, default `Member`) opens no section, whatever is stored on
  it, and its page refuses sections (D71).
- **Seeded:** an `Editor` role with Content and Media, once per database (`SeedEditorRole`, marker `GlobalData`
  `ZauberEditorRoleSeeded`), and only when no role of that name exists: a site's own `Editor` (perhaps a front-end
  members' role) is left as it is, and a deleted one does not come back. A plugin section is in no role until an
  administrator ticks it.
- **The decision** is `IBackOfficeAccessService` (`ZauberCMS.Core.Sections.Interfaces`, scoped, replaceable):
  `GetSectionsAsync(user)`, `CanOpenSectionAsync(user, alias)`, `GetSectionForPath(path)`, and the user and role
  rules below. The role → sections list is cached under the `Role` prefix, cleared by every role save and delete
  (with the restricted-media lookup, which compares role names too). For pages and the navigation a user's roles are
  the ones their sign-in carries, as for every other role check: a page request renews them every five minutes, an
  open admin tab at its next full load (a circuit checks the security stamp every ten minutes), so a role renamed or
  taken away reaches an open session then. The checks that could give access (users, roles, sections) read the
  editor's roles as stored, not the sign-in's.
- **Pages.** `[AuthorizeSection("Alias", …)]` (an `[Authorize]` whose policy, `SectionPolicies.ForSections(...)`,
  the ZauberCMS policy provider builds by name: no registration) lets in whoever may open any of the named sections;
  with no alias, anyone who may open at least one (`SectionPolicies.BackOffice`). The pages in each
  `Components/Admin/<Section>/` folder get it from that folder's `_Imports.razor`; the SEO pages carry Settings. A page
  under `/admin` that declares nothing (`[Authorize]`/`[AllowAnonymous]`) is for the users of the section whose
  `IndexUrl` its address is under (longest match, whole segments; `/admin` itself and an address under no section:
  administrators only), in `AdminAccessMiddleware` for requests and `Routes.razor` for navigation in a circuit.
- **Landing and refusal.** A request for `/admin` is redirected by `AdminAccessMiddleware` to the first section the
  user may open (navigating to `/admin` inside a circuit, `Admin/AdminIndex.razor` does the same); signing in
  without a return address sends anyone with a section there, and with one (a page that sent the visitor to sign
  in) back to it when it is on this site (ZA-201). A refused signed-in user goes to
  `/account/accessdenied` (the cookie's default access-denied address, status 403; links to the admin and to sign in
  as someone else); nobody signed in goes to the login page.
- **Navigation and extensions.** `SectionLinks` shows the user's sections only. A nav item (`ISectionNav`), nav group
  action (`ISectionNavGroupAction`), dashboard (`ISectionDashboard`) or tree context menu (`ITreeContextMenu`) whose
  type carries `[Authorize]`/`[AuthorizeSection]` shows only to users who pass it (a nav group left with no item is
  hidden): a plugin keeps an administrators-only tool in a section other roles open with
  `[Authorize(Roles = Constants.Roles.AdminRoleName)]` on its type. The profile menu's "Edit" (the user's page in
  Users) shows only to users who may open Users.
- **Endpoints.** Recycled and role-restricted media files are served to whoever may open Media (before D38: Admin
  only). A controller or minimal API the admin calls carries `[AuthorizeSection(...)]` or
  `.RequireAuthorization(SectionPolicies.ForSections(...))`.
- **Administrators only, whoever else may open Settings (D70):** Global Settings (who gets in, uploads, API keys): its
  page and nav item carry `[Authorize(Roles = Admin)]`.
- **Users and roles without widening access (D68), as Umbraco's user and user-group editors check:** someone who may
  open Users but is not an administrator cannot change, delete, set the password of, or change the roles of an
  administrator, or of a user who opens a section they cannot (that would hand them the section); can give only
  roles they are in (never `Admin`); can open and save only roles they are in (never
  `Admin`) and give a role only sections they can open; can make a new role with those sections, and is put in it
  (as Umbraco puts a group's maker in the group), so they can go on managing and giving it. The pages ask
  `CanEditUserAsync`, `CanSetUserRolesAsync`, `CanEditRoleAsync`, `CanSaveRoleAsync`, `CanDeleteRoleAsync` before
  each change (D27: the services do not check), and the dialogs disable what cannot be given. The `Admin` role
  cannot be renamed or deleted by anyone (`IMembershipService` refuses, as it refuses removing the last
  administrator). No role that pages or media are restricted to can be deleted (it would open them to everyone,
  ZA-202); the role page asks before it deletes.
- **A plugin section:** implement `ISection` (its index page, as below), put its pages under its `IndexUrl`, and
  they are opened to the section's users without an attribute; or put `[AuthorizeSection("MySection")]` on each
  (needed for a page elsewhere, and for a controller). A page meant for administrators only says
  `[Authorize(Roles = Constants.Roles.AdminRoleName)]`. A site that registers its own
  `IAuthorizationPolicyProvider` derives it from `SectionAuthorizationPolicyProvider`, or the section policies are
  not found.

## Building blocks

| Need | Implement | Copy |
| --- | --- | --- |
| Top-level section (nav button + index page) | routable page `@implements ISection` (Name, Alias, IndexUrl, SortOrder); roles are given it on the role page, pages under `IndexUrl` follow it ("Access") | `Admin/ContentSection/ContentIndex.razor` |
| Sidebar group | `ISectionNavGroup` (Heading, Alias, SortOrder, SectionAlias) | `ContentNavGroup.cs` (its `Actions` is obsolete and never read: group actions are `ISectionNavGroupAction`s) |
| Sidebar item/tree | component `@implements ISectionNav` (SortOrder, SectionNavGroupAlias, **Dispose**) | `Admin/SettingsSection/Navigation/SettingsTree.razor` |
| Group "…" menu action | `ISectionNavGroupAction` (`ContextMenuAction(...)`; call `contextMenuService.Close()` first) | `ContextMenus/SectionNavGroups/CreateContentNavMenu.cs` |
| Tree right-click item | `ITreeContextMenu` — shown when `Sections` contains the current section or `TreeAlias` matches; gate with `CanShowContextMenu` | `ContextMenus/ContentMenus/CopyContentContextMenu.cs` |
| Dashboard tab | component `@implements ISectionDashboard` (TabName, SortOrder, SectionAlias) | `Admin/Dashboards/LatestUsersDashboard.razor` |

Tree aliases: `Constants.Sections.Trees`. Trees are built on `Trees/BaseTree.razor` / `GenericTree` / `ContentTree` /
`MediaTree`, plus `Shared/TreeButton.razor` for single links — not raw `RadzenTree`.

Tree data (Phase 10, D26): `ContentTree` and `MediaTree` load and refresh themselves (leave `Data` empty) a page of a
node at a time through `IContentService.GetContentTreeLevelAsync` / `IMediaService.GetMediaTreeLevelAsync`, which
also say which items have children; the rest of a node is a `TreeLoadMore` row ("Show 100 more"). The row is a
button (`TreeExtensions.Create…TreeTemplate(this, LoadMoreAsync)`) whose click never selects it, so the selected
node keeps its highlight (ZA-161); a tree still handles the row being selected, which the keyboard can do. `HasChildren` and
`ShouldBeExpanded` are called while rendering: they must read memory (each tree keeps the flags it loaded), never
query.
`ContentTypeTree` loads the whole type tree once (`OnlyElementTypes` / `OnlyCompositions` pick the kind). Tree nodes
carry no property values: a context menu that needs them loads the item by id. A level that mixes item types sets
`Text = TreeExtensions.TreeNodeText`, not `TextProperty` (Radzen reads it as the first item's type).

Recycle bins: Content has `RecycleBinTree`, Media has `MediaRecycleBinTree` (`Admin/MediaSection/Navigation/`).
Both load through the same tree-level methods with `InRecycleBin = true` (ZA-156): the top of the bin (items whose
parent is not binned) a page at a time, most recently binned first, with "Show more"; a binned page or folder opens to
show what is in the bin below it. Restoring an item whose parent is still in the bin is refused by the service
("Restore the parent … first"); a permanent delete removes the item and everything below it.
Both set `DisableSectionOnlyContextMenu`, so only menus listing their `TreeAlias` show there — a section-wide menu
(`Sections = [MediaSection]`, such as Delete or Move) never acts on a binned item. A plugin menu for binned media
adds `Constants.Sections.Trees.MediaRecycleBinTree` to its `TreeAlias`; the item is the `Media` (root:
`TreeStub` with `Constants.Guids.MediaRecycleBinRootId`). In the content bin the item is a `TreeBranch` (its `Id` is
the page's; root: `TreeStub` with `Constants.Guids.RecycleBinRootId`), at any depth; the "Show more" row is a
`TreeLoadMore`. The built-in actions are in `ContextMenus/MediaRecycleBinMenus/` and `ContextMenus/RecycleBinMenus/`.

## Errors in the admin (D32)

One exception in an editor, dashboard or plugin no longer ends the circuit. `ZauberCMS.Components.Shared.AdminErrorBoundary`
(an `ErrorBoundary`) shows "*Name* could not be shown" with a **Try again** button in place of what failed; the rest
of the page, its unsaved changes included, carries on. Boundaries sit round each property editor
(`DynamicContentProperty`), each dashboard tab, the sidebar navigation and the page body (`SectionLayout`; cleared
on navigation), and the vendored modal puts one round each side panel's content (with **Close**). Radzen's own
dialogs and side panels (`DialogService`) are inside one round `<RadzenComponents />`: an error there closes the
open Radzen dialogs and shows a box over the page with **Close**. The exception is
logged; its message is shown only when Blazor's detailed errors are on (`CircuitOptions.DetailedErrors`, which
`Zauber:ShowDetailedErrors` outside Production, or the `DetailedErrors` setting, turns on), otherwise "The details are in the site's log".
A property editor that fails keeps the value it last reported, so saving the page does not lose it. Block previews
already render apart (`BlockPreview` catches a preview that throws). A plugin page can use `<AdminErrorBoundary
Name="The report">…</AdminErrorBoundary>` round its own parts (`Describe(exception)` gives the same text for an `ErrorContent` of its own). Errors in event handlers and lifecycle methods are
caught; work started without awaiting it (`async void`, `Task.Run`) is not.

## Dialogs, confirms, notifications

```csharp
var modal = ModalService.OpenSidePanel<MyDialog>("Title", new Dictionary<string, object> { ["Id"] = id });
var result = await modal.Result;
if (result is { Confirmed: true, Data: MyType value }) { … }
```

- Dialog: `[CascadingParameter] BlazoredModalInstance BlazoredModal { get; set; } = default!;` close with
  `BlazoredModal.CloseAsync(ModalResult.Ok(value))` / `BlazoredModal.CloseAsync(ModalResult.Cancel())`. Put dialogs in a `Dialogs/` folder.
  The modal is the vendored Blazored.Modal source in `ZauberCMS.Components/Modal/` (not a NuGet package; namespaces
  `Blazored.Modal*`): panels nest, and fixes go there.
- Yes/no: Radzen `DialogService.Confirm(...)`.
- Notifications: `NotificationService.ShowSuccessNotification/ShowErrorNotification/ShowWarningNotification/
  ShowNotifications(handlerResult.Messages)` (`ZauberCMS.Components/Core/Extensions/NotificationServiceExtensions.cs`).
- Form layout: `<EditorRow>` with `<LeftColumn><PropertyInfo Name Alias Description/></LeftColumn><CentreColumn>…`
  (`Shared/EditorRow.razor`, `Shared/PropertyInfo.razor`).

## State & events

- `AppState` (singleton, weak refs): subscribe in `OnInitialized` with a method group, unsubscribe in `Dispose`.
  Handlers run on the publisher's thread in every admin circuit → `await InvokeAsync(StateHasChanged)`.
  Who raises which event: [Services.md → AppState events](Services.md#appstate-events). Don't double-notify.
- `TreeState` (scoped per circuit): selected node, expanded nodes, `CurrentSection`, and an old shared child cache
  (`HasChildrenCache`, `ClearChildCache`) that the built-in content, media and content-type trees no longer use.

## Styling

Radzen (material theme) + Tailwind compiled into `wwwroot/css/admin.css`. Tailwind purges by scanning `**/*.razor`:
after adding classes run `npm run site-css` in `ZauberCMS.Components`. Plugin assemblies can only use classes already
in the compiled CSS (or ship their own stylesheet).

## Rules

- An admin page outside the section folders of `Components/Admin/` declares its access itself (`[AuthorizeSection]`,
  `[Authorize]`), unless its address is under its section's `IndexUrl` ("Access"). The page is the check: the
  services don't decide who may write ([Services.md](Services.md) "Callers and access"), so a page or menu opened to
  a section's users can do whatever the services it calls can do; a change that could widen someone's access asks
  `IBackOfficeAccessService` first.
- Never call EF directly from components — use services.
- Every service save result goes to the user via the notification extensions; never show raw exception text.
