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
  and the `AllowedAdminIpAddress` check (non-whitelisted → forced logout via `Urls.ApiLogout`).
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
`MediaTree` / `TreeButton`, not raw `RadzenTree`.

## Dialogs, confirms, notifications

```csharp
var modal = ModalService.OpenSidePanel<MyDialog>("Title", new Dictionary<string, object> { ["Id"] = id });
var result = await modal.Result;
if (result is { Confirmed: true, Data: MyType value }) { … }
```

- Dialog: `[CascadingParameter] BlazoredModalInstance BlazoredModal { get; set; } = default!;` close with
  `BlazoredModal.CloseAsync(ModalResult.Ok(value))` / `BlazoredModal.CloseAsync(ModalResult.Cancel())`. Put dialogs in a `Dialogs/` folder.
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
