# Admin UI

`/admin` is `InteractiveServer` + Radzen + Blazored.Modal. Code lives in `ZauberCMS.Components/` (paths below are
relative to it unless prefixed). Everything is discovered by interface (see AGENTS.md → Extension points); public
non-abstract types only, keyed by `Type.FullName`. Metadata properties (names, aliases, sort orders) are read from
instances created by `ActivatorUtilities` — `[Inject]` is **not** populated there, so keep metadata as plain values.

## Page skeleton

```razor
@page "/admin/myfeature"
@layout SectionLayout
@attribute [Authorize(Roles = Constants.Roles.AdminRoleName)]   @* automatic only inside Components/Admin/ *@

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
- Access: a page under `/admin` is admin-only unless it says otherwise. Pages in `ZauberCMS.Components/Admin/` get
  `[Authorize(Roles = Admin)]` from `_Imports.razor`; a page anywhere else (your site, a plugin) with no
  `[Authorize]` or `[AllowAnonymous]` is treated the same way by `AdminAccessMiddleware` and `Routes.razor`.
  To open an admin page to another role, put `@attribute [Authorize(Roles = "Editor")]` on it.
  The admin IP allow-list (`GlobalSettings.AllowedAdminIpAddress`) is enforced by the middleware: 403.
  The layout is cascaded with `IsFixed="true"`: use it to call `SetSection`, don't read changing state from it.
- An editor page loads its model when its route values change, not on every `OnParametersSetAsync` — parameters
  are set again whenever a parent re-renders, and reloading there drops unsaved edits (see `ContentTypeEditor`).
- Context-menu actions (`ITreeContextMenu`, `ISectionNavGroupAction`) are run by `BaseTree` / `SectionTrees`, which
  log an exception and show a generic error. Still report expected failures yourself (`HandlerResult.Messages`).
- Routes are found because `Routes.razor` passes `ExtensionManager.GetFilteredAssemblies(null)` as additional assemblies.
- Admin URLs live in `ZauberCMS.Core/Urls.cs` (e.g. `Urls.AdminUpdateContent`). Plugins use their own constants.

## Building blocks

| Need | Implement | Copy |
| --- | --- | --- |
| Top-level section (nav button + index page) | routable page `@implements ISection` (Name, Alias, IndexUrl, SortOrder) | `Admin/ContentSection/ContentIndex.razor` |
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
  The modal is the vendored Blazored.Modal source in `ZauberCMS.Core/Modal/` (not a NuGet package): panels nest, and fixes go there.
- Yes/no: Radzen `DialogService.Confirm(...)`.
- Notifications: `NotificationService.ShowSuccessNotification/ShowErrorNotification/ShowWarningNotification/
  ShowNotifications(handlerResult.Messages)` (`ZauberCMS.Core/Extensions/NotificationServiceExtensions.cs`).
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

- Admin pages outside `Components/Admin/` must add the `[Authorize]` attribute themselves. The page is the check:
  the services don't decide who may write ([Services.md](Services.md) "Callers and access"), so a page or menu
  opened to another role can do whatever the services it calls can do.
- Never call EF directly from components — use services.
- Every service save result goes to the user via the notification extensions; never show raw exception text.
