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
- `Admin/Layout/SectionLayout.razor`: top nav (`SectionLinks`), sidebar trees (`SectionTrees`), the cascading modal,
  and the single `<RadzenComponents />` host (a second one doubles every dialog and notification).
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
| Sidebar group | `ISectionNavGroup` (Heading, Alias, SortOrder, SectionAlias) | `ContentNavGroup.cs` (ignore its unused `Actions`) |
| Sidebar item/tree | component `@implements ISectionNav` (SortOrder, SectionNavGroupAlias, **Dispose**) | `Admin/SettingsSection/Navigation/SettingsTree.razor` |
| Group "…" menu action | `ISectionNavGroupAction` (`ContextMenuAction(...)`; call `contextMenuService.Close()` first) | `ContextMenus/SectionNavGroups/CreateContentNavMenu.cs` |
| Tree right-click item | `ITreeContextMenu` — shown when `Sections` contains the current section or `TreeAlias` matches; gate with `CanShowContextMenu` | `ContextMenus/ContentMenus/CopyContentContextMenu.cs` |
| Dashboard tab | component `@implements ISectionDashboard` (TabName, SortOrder, SectionAlias) | `Admin/Dashboards/LatestUsersDashboard.razor` |

Tree aliases: `Constants.Sections.Trees`. Trees are built on `Trees/BaseTree.razor` / `GenericTree` / `ContentTree` /
`MediaTree`, plus `Shared/TreeButton.razor` for single links — not raw `RadzenTree`.

Recycle bins: Content has `RecycleBinTree`, Media has `MediaRecycleBinTree` (`Admin/MediaSection/Navigation/`).
Both set `DisableSectionOnlyContextMenu`, so only menus listing their `TreeAlias` show there — a section-wide menu
(`Sections = [MediaSection]`, such as Delete or Move) never acts on a binned item. A plugin menu for binned media
adds `Constants.Sections.Trees.MediaRecycleBinTree` to its `TreeAlias`; the item is the `Media` (root:
`TreeStub` with `Constants.Guids.MediaRecycleBinRootId`). The built-in actions are in `ContextMenus/MediaRecycleBinMenus/`.

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
- `TreeState` (scoped per circuit): selected node, expanded nodes, child cache (`ClearChildCache`), `CurrentSection`.

## Styling

Radzen (material theme) + Tailwind compiled into `wwwroot/css/admin.css`. Tailwind purges by scanning `**/*.razor`:
after adding classes run `npm run site-css` in `ZauberCMS.Components`. Plugin assemblies can only use classes already
in the compiled CSS (or ship their own stylesheet).

## Rules

- Admin pages outside `Components/Admin/` must add the `[Authorize]` attribute themselves.
- Never call EF directly from components — use services.
- Every service save result goes to the user via the notification extensions; never show raw exception text.
