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
  `[Authorize(Roles = Constants.Roles.AdminRoleName)]` on its type.
- **Your account (D72, Umbraco's current-user panel).** The profile menu's "Your account" opens
  `Admin/Layout/CurrentUserPanel.razor` for every back-office user: their name (changed at once, no session ends),
  their email address (a link goes to the new address and the account moves when it is opened; needs the current
  password) and their password (the current one, then the new one twice; the other sessions end and this page
  reloads through the sign-in refresh). Nothing else: roles, sections and profile properties stay on the user's page
  in Users, which the panel links to for users who may open Users (on their own page there the address box is
  read-only and "Change Password" opens the panel). The panel acts only on the signed-in user, read from their
  sign-in for each change, never from a parameter (D27); a wrong current password counts towards the lockout as at
  sign-in, one link a minute is sent per account, and only the newest link works. The sign-in renewal after a
  password change (`/api/auth/refreshsignin` with the change's ticket) also takes a cookie the five-minute stamp
  check has just refused (ZA-218). The service side is
  `IMembershipService.ChangeUserNameAsync`, `ChangePasswordAsync` and `RequestEmailChangeAsync`; a plugin's own
  account page can call them the same way, for the signed-in user only.
- **Endpoints.** Recycled and role-restricted media files are served to whoever may open Media (before D38: Admin
  only). A controller or minimal API the admin calls carries `[AuthorizeSection(...)]` or
  `.RequireAuthorization(SectionPolicies.ForSections(...))`.
- **Administrators only, whoever else may open Settings (D70):** Global Settings (who gets in, uploads, API keys): its
  page and nav item carry `[Authorize(Roles = Admin)]`. Its API keys show names only (D73): a saved value is never
  sent back to the page, an empty box keeps it, a typed one replaces it; keys set in configuration
  (`Zauber:ApiKeys`, which win) are listed by name.
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
on navigation), and the vendored modal puts one round each side panel's content (with **Close**). The access
checks that read the roles on a cache miss are covered too (ZA-215): the header's `SectionLinks` shows "The
sections could not be shown: try again" in its place, and `Routes.razor` puts a boundary ("This page") round the
admin's route and its `AuthorizeView`/`AuthorizeRouteView`, cleared by the next navigation, for a check that fails
before the layout is there. Radzen's own
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
  ShowNotifications(handlerResult.Messages, L)` (`ZauberCMS.Components/Core/Extensions/NotificationServiceExtensions.cs`;
  with `L` the summary is the message type in the admin's language, "Localisation" below).
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

## Localisation (Roadmap D37, D90–D97)

Every text ZauberCMS shows (the admin, the installer, the account pages and its fallback site pages) comes from
`IStringLocalizer<ZauberText>` (`ZauberCMS.Core.Localization`), and ZauberCMS ships English. It is .NET
localisation, so it follows `CultureInfo.CurrentUICulture`: in the admin `Zauber:AdminDefaultLanguage` (D37: one
admin language, not one per user), on the account and site pages the request's culture. A site's own text (its
views, its dictionary items: `LanguageDictionary`, `EntryModel.LanguageKeys`) is content, not this.

**How it works.** The texts are `.resx` files in a folder named `ZauberText`, one file per area. ZauberCMS's own are
`ZauberCMS.Components/Resources/ZauberText/<Area>.resx` (the admin's text) and
`ZauberCMS.Core/Resources/ZauberText/<Area>.resx` (Core's messages, the installer); they are the neutral (English)
files, embedded in the assemblies, so they ship in the packages. A key is looked up in the most specific culture
first (`fr-CA`, then `fr`, then the neutral files), and at each step in the site's entry assembly first, then in the
plugins in the order passed to `AddZauberCms(...)`, then in ZauberCMS's (`ZauberTextCatalog`, internal; every file is
read once). A key nobody has shows as the key itself and is logged once as a warning. Radzen's own text (a grid's
"No records to display.", its filter and pager, date pickers) comes through the same files: Radzen's key `X` is
`radzen_X` (`DataGrid_EmptyText` → `radzen_DataGrid_EmptyText`, Radzen's keys are in its `RadzenStrings`
resource); ZauberCMS ships no Radzen file, so a key nobody translated is Radzen's built-in English (D93).

**A site or a translator adds a language (D90).**

1. In the site project, or a plugin or language-pack class library that the site passes to
   `builder.AddZauberCms(typeof(SomeTypeInIt).Assembly)`, make a folder named `ZauberText` (`Resources/ZauberText/`).
2. Add a file per area with the culture in its name: `General.fr.resx`, `ContextMenus.fr.resx`, … holding the keys
   it translates, named as in ZauberCMS's English files (copy the file, translate the values, keep the `{0}`s). A
   key it leaves out stays English. The SDK builds them into a satellite assembly (`fr/MySite.resources.dll`),
   which a NuGet package ships too: no build chain beyond `dotnet build`. Name the file with a culture .NET knows
   (`da`, not `dk`; `en-GB`, not `en-UK`): the SDK builds a file named for any other into the assembly itself, and
   ZauberCMS does not use it and logs a warning naming it, so it never replaces the English (D162).
3. Set `Zauber:AdminDefaultLanguage` to `fr`. The account pages use the request's culture, so they show French to a
   request in a culture the site has (`UseRequestLocalization`: the site's languages as they are now, so a language
   added in Settings > Languages is one without a restart, D161).
4. To change ZauberCMS's English wording, `General.en.resx` with the keys to change. A file with no culture
   (`General.resx`) changes a key in every language that has no translation of it.
5. Radzen: `Radzen.fr.resx` with `radzen_DataGrid_EmptyText` and the other keys it translates.
6. Texts kept elsewhere (a database, JSON): register an `IStringLocalizer<ZauberText>` of your own, as a singleton
   (Radzen's bridge holds it), after `AddZauberCms` (it replaces ZauberCMS's; registered before, ZauberCMS's is not
   added), and a Radzen `ILocalizer` if Radzen's text comes from there too. Radzen's bridge reads the `radzen_*`
   texts through its `GetAllStrings(true)`, once per culture: one that throws leaves Radzen its own English (a
   warning is logged), and the first of a name given twice wins.

A plugin with admin text of its own does the same with its own area: `ZauberText/MyPlugin.resx` (English) with keys
`myPlugin_*`, read with `IStringLocalizer<ZauberText>`, and a site or translator adds `MyPlugin.fr.resx`. (A plain
`IStringLocalizer<T>` of its own also works, but is not part of these layers.)

**Rules for ZauberCMS's code (D91, D92, D97).** Phase 10h's parts follow these literally.

1. **Key**: `area_name`. `area` is the file name with a lower-case first letter (`ContextMenus.resx` → `contextMenus_`),
   `name` is camelCase and says what the text means (`contextMenus_moveToRecycleBin`, `general_save`), not where it
   is. The `.resx` entry's name is the whole key. Keep today's English as the value, trimmed, without markup (split
   `<strong>Term</strong> - text` into two keys); fix an obvious typo.
2. **File**: the table below says which file a folder's text goes in. A key is in one file only. A part adds keys
   only to its own area files (it may create a new file for a folder it owns); `General.resx` is the shared
   vocabulary (buttons, labels, notification summaries): use its keys when the English is exactly the same, and
   put a missing word in your own area, not in General.
3. **Write the key as a string literal at the indexer**: `L["general_save"]`, never from a variable or constant;
   the guard test reads the literals.
4. **Placeholders**: one key per sentence, `L["contextMenus_deleteConfirm", page.Name]` with the value `Delete {0}?`
   and a `<comment>` naming each placeholder (`{0} = the page name`); numbered from `{0}`; never concatenate
   localised fragments or wrap localised text in `$"..."`. A comment that says where a text is shown helps the
   translator. A count is two keys, as English needs: `…One` for a count of one (the sentence without the number) and
   the key itself for any other count, chosen in code with each key a literal
   (`count == 1 ? L["contextMenus_itemsDeletedOne"] : L["contextMenus_itemsDeleted", count]`); a translator writes
   both (D160; plural forms for other languages are ZP-030).
5. **Razor**: every component in ZauberCMS.Components has `L` (`@inject IStringLocalizer<ZauberText> L` in
   `ZauberCMS.Components/_Imports.razor`): `Text="@L["general_save"]"`, `@L["dashboards_recentActivity"]`, and
   `L["..."]` in `@code`. `L[...]` is a `LocalizedString`, which converts to `string`; write `.Value` where the target
   is `object` or a `var` that must be a string. Core's installer components add the same line to their own
   `_Imports.razor`.
6. **A type discovery builds** (`ExtensionManager.GetInstances`), whose text is read from that instance, gets the
   localiser **by constructor**: such an instance has no `[Inject]` properties. That is `ISection.Name`,
   `ISectionDashboard.TabName`, `ISectionNavGroup.Heading`, `ITreeContextMenu.Text`, `ISectionNavGroupAction.Text`,
   `IDataListSource.Name`/`Description`, `IContentProperty.Name`/`Description`, an SEO check's text. In a `.cs`
   class: `IStringLocalizer<ZauberText> text` as the last parameter of the primary constructor,
   `public string Heading => text["sections_contentGroup"];`. In a `.razor` component: a constructor in `@code`
   (Blazor builds components with constructor injection too), the markup still uses `L`:
   ```razor
   @code {
       private readonly IStringLocalizer<ZauberText> _text;
       public MainContentDashboard(IStringLocalizer<ZauberText> text) => _text = text;
       public string TabName => _text["dashboards_latestContent"];
   }
   ```

   A built-in property editor also keeps its parameterless constructor (an RTE toolbar item its `(IModalService)`
   one), for code that reads only its alias or type (`new TextProperty().Alias`): it passes `EditorText.English`
   (ZauberCMS's English) to the localising one, which is marked `[ActivatorUtilitiesConstructor]` so discovery and
   Blazor choose it; an editor's `Name` and `Description` keep their setters, and a value set wins (D120).
7. **Notifications**: `NotificationService.ShowNotifications(result.Messages, L)` (the summary in the admin's
   language); a summary of your own: `L["general_success"]` / `L["general_error"]`; Radzen confirms:
   `new ConfirmOptions { OkButtonText = L["general_yes"], CancelButtonText = L["general_no"] }`.
8. **Core messages (D92)**: a Core service, validator or check that builds text shown to a person injects
   `IStringLocalizer<ZauberText>` (the last constructor parameter: a constructor change for the v5 release notes)
   and writes the text there, `result.AddMessage(text["content_saved"], ResultMessageType.Success)`. Its keys are in
   Core's file of the area (`ZauberCMS.Core/Resources/ZauberText/<Area>.resx`): Core's code uses Core's keys only (it
   runs without Components' files). `HandlerResult` and `ResultMessage` carry the text, already in the caller's
   culture, as before: no keys, no API change. An `IValidate<T>` takes it by constructor (rule 6); FluentValidation:
   `.WithMessage(_ => text["…"])`, so each call reads the current culture. Static helpers take it as a parameter;
   `SaveChangesAndLog`'s messages come from the `ExtensionManager` it is given (`ExtensionManager.Text`, internal),
   so its callers do not change (D122). A message that travels in an exception because it is written for the person
   at a form is localised too (`DatabaseSetupParameters.BuildConnectionString(text)`, D121).
   DataAnnotations (only on the account parameter classes and `NavigationItem`; Blazor shows an attribute's message
   as written): the `Zauber*` attributes of `Membership/Validation` (`[ZauberRequired]`, `[ZauberEmailAddress]`,
   `[ZauberStringLength]`, `[ZauberComparePassword]`) keep the base attribute's rule and read the localiser from
   `ValidationContext.GetService`, which Blazor's `DataAnnotationsValidator` fills from the page's services; without
   services they give the base attribute's English (D130); a field's name in the message is a key of its own
   (`AccountValidationText.FieldName`). ASP.NET Identity's own messages go through
   `ZauberIdentityErrorDescriber` (`membership_identity*`, D131). An email's paragraph is
   `EmailHtml.Paragraph(text["email_…"], openTag, closeTag)`: the sentence HTML-encoded, the link's tags as
   `{0}`/`{1}` (D132); a translation whose placeholders do not match is logged and Core's English is used, so the
   link is never lost. The one account-page message with a link (`membership_passwordResetDone`, shown as markup by
   `StatusMessage`) is built the same way (ZA-304).
9. **Not text**: log messages and exception messages (English, for whoever reads the log); aliases, icon names, CSS,
   URLs; values that are stored and shown later as data (an audit row's description stays English, as Umbraco's
   audit comments do; the `"Unknown"` user name passed to `AppState` events; `CountriesDataSource`'s country names,
   which are the stored values). A guard-test exception (`NotText`) names anything else that only looks like text.

| Area (file) | Prefix | Text of | Files | Part |
| --- | --- | --- | --- | --- |
| General | `general_` | shared words: buttons, labels, notification summaries | Components | 10h-1 (filled; the others read it) |
| Sections | `sections_` | section names, nav group headings (the Users ones are there for 10h-4) | Components | 10h-1 |
| Dashboards | `dashboards_` | `Admin/Dashboards` | Components | 10h-1 |
| Shared | `shared_` | `Shared`, `Admin/Shared`, `Modal`, `DataSources`, `Core` (Components'), `ContentComponents`, `Startup` | Components | 10h-1 |
| Trees | `trees_` | `Trees` | Components | 10h-1 |
| ContextMenus | `contextMenus_` | `ContextMenus` (tree menus, nav group actions) | Components | 10h-1 |
| Content | `content_` | `Admin/ContentSection`; Core `Content` (services, validators) | Components, Core | 10h-2 |
| Media | `media_` | `Admin/MediaSection`; Core `Media`, `Providers` (storage) | Components, Core | 10h-2 |
| EditorDialogs | `editorDialogs_` | `Editors/Dialogs` | Components | 10h-2 |
| Seo | `seo_` | `Seo`; Core `Seo` (checks, messages) | Components, Core | 10h-2 |
| Editors | `editors_` | `Editors` (root, `Settings`, `Components`, `Models`, `ToolbarItems`, `Interfaces`), the API Key Picker included (its keys moved from Settings at the 10h merge: `EditorText` reads only this file, D120) | Components | 10h-3 |
| Installer | `installer_` | Core `Installer` (its components and messages) | Core | 10h-3 |
| Shared (Core) | `shared_` | Core `Shared`, `Extensions` (the save messages), `Data` | Core | 10h-3 |
| Structure | `structure_` | `Admin/StructureSection` | Components | 10h-4 |
| Settings | `settings_` | `Admin/SettingsSection` | Components | 10h-4 |
| Users | `users_` | `Admin/UsersSection` | Components | 10h-4 |
| Layout | `layout_` | `Admin/Layout`, `Admin/AdminIndex.razor`, `Admin/DynamicContentProperty.razor` (an editor's boundary name), the admin boundary in `ZauberCMS/Components/Routes.razor` | Components | 10h-4 |
| Account | `account_` | `Account/*`, `Pages` (the fallback site pages), `Controllers` (the sign-out page) | Components | 10h-4 |
| Languages | `languages_` | Core `Languages` | Core | 10h-4 |
| Membership | `membership_` | Core `Membership`, `Security` (users, roles, sign-in, registration, the account forms' validation, ASP.NET Identity's messages) | Core | 10h-4 |
| Sections (Core) | `sections_` | Core `Sections` (`BackOfficeAccessService`'s reasons) | Core | 10h-4 |
| Audit, Tags, Email | `audit_`, `tags_`, `email_` | Core `Audit`, `Tags`, `Email` (the emails' text) | Core | 10h-4 |
| Radzen | `radzen_` | Radzen's own text: no ZauberCMS file, only sites' and language packs' | — | — |

An area with files in both projects is one area (a translator writes one `Content.fr.resx`); a key is in one of
them. Each part converts only its own folders and files; `Admin/Layout`, `Admin/UsersSection`, `Account/*`, the
API-key picker editor and `ZauberCMS/Components/Routes.razor` (left out of 10h-1 while 10g-2 edited them) are
10h-4's.

**The guard (D94)**, `tests/ZauberCMS.Core.Tests/Localization/ZauberTextResourceTests.cs`: every key is named after
its file and is in one file once; texts are trimmed and their placeholders numbered from `{0}`, and the English
writes SEO and URL in capitals; a translation has only keys of the English with the same placeholders; every literal
key the code of Core, Components and `ZauberCMS` asks for is in the English files (Core's in Core's), every
non-General key is used; every literal key is read with one argument per placeholder (the arguments of
`EmailHtml.Paragraph` and `InstallerMarkup.WithCode` count for the key they wrap; `FormattedByCaller` lists the
others) and every text with placeholders formats with that many (ZA-303); every English text is in the built
assemblies; and the folders listed in
`ConvertedFolders` keep no hard-coded text (markup text, text attributes, literals given to UI members and calls,
sentences). A part adds its folders to its own block of `ConvertedFolders` (and `NotText`), so the parts merge
cleanly. `ZauberTextTests` covers the lookup order and the bootstrap. A component test that renders with its own
`ServiceCollection` registers the text too (every component has `L`): `AdminRenderer.AddImportedServices` in
`tests/ZauberCMS.Components.Tests/Shared` does it, with the Core services `_Imports.razor` injects.

## Rules

- An admin page outside the section folders of `Components/Admin/` declares its access itself (`[AuthorizeSection]`,
  `[Authorize]`), unless its address is under its section's `IndexUrl` ("Access"). The page is the check: the
  services don't decide who may write ([Services.md](Services.md) "Callers and access"), so a page or menu opened to
  a section's users can do whatever the services it calls can do; a change that could widen someone's access asks
  `IBackOfficeAccessService` first.
- Addresses are site paths: an `ISection.IndexUrl`, a nav item's or tree item's `Url` and a `TreeButton`'s `Url`
  start with `/` and are read from the site's root, so they keep a path base (`UsePathBase`, D101). A plugin's own
  `NavigateTo`, `href`, `src` or Radzen `Path` to one goes through `NavigationManager.ToSiteUrl(...)`
  (`ZauberCMS.Core.Extensions`); a `returnUrl`/`redirectUrl` it passes is `NavigationManager.GetReturnUrl()`.
  `NavigateTo("/admin/…")` and `href="/admin/…"` leave the path base.
- Never call EF directly from components — use services.
- Every service save result goes to the user via the notification extensions; never show raw exception text.
