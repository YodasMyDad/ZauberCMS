# Property Editors

Paths relative to `ZauberCMS.Components/` unless prefixed. Reference set to copy: `Editors/NumericProperty.razor` +
`Editors/Settings/NumericPropertySettings.razor` + `Editors/Models/NumericPropertySettingsModel.cs`. For an editor
that opens a dialog: `Editors/EditorNotesProperty.razor` + `Editors/Dialogs/EditorNotesDialog.razor`.

## Files

| File | Implements |
| --- | --- |
| `Editors/XProperty.razor` | `IContentProperty` (`ZauberCMS.Core/Content/Interfaces/IContentProperty.cs`) |
| `Editors/Models/XSettingsModel.cs` (optional) | plain class, public get/set, parameterless ctor |
| `Editors/Settings/XSettings.razor` (optional) | `IContentPropertySettings<XSettingsModel>` |

## Editor contract

- **Metadata** (`Name`, `Alias`, `Description`, `Icon`, `SettingsComponent`, `Scripts`, `Styles`, `FullWidth`) is read
  from an instance built without DI — plain initialisers only.
- `Alias` must be globally unique (the "Add Property" picker resolves by it). Third-party editors must not use the
  `ZauberCMS.` prefix.
- Required `[Parameter]`s — `DynamicContentProperty` always passes them and throws if one is missing:
  `string? Value`, `EventCallback<string> ValueChanged`, `string? Settings`, `Content? Content`.
- Optional: `[Parameter] string? PropertyAlias`; implement `ISupportsBlockListChanges` to receive `ChangesPending`
  (see [BlockListEditor.md](BlockListEditor.md)).
- `[CascadingParameter] IModalService? ModalService` for dialogs (`ModalService.OpenSidePanel<T>(…)`, then `await modal.Result`).
- `Content` is **null** when the editor renders for User/Role properties — handle it.
- `SettingsComponent = null` → admin shows "No settings available".
- `Scripts`/`Styles` are emitted on every `/admin` page; use `_content/{AssemblyName}/…` paths.

## Persistence

- A property type stores `Component = editorType.FullName` and `ComponentAlias = Alias`. **Renaming the class or
  namespace orphans every existing property** using it (the sitemap also hard-codes
  `ZauberCMS.Components.Editors.SeoProperty`).
- The value is always a **string**; complex values are JSON (pickers store `List<Guid>`). Emit with
  `await ValueChanged.InvokeAsync(value)`.
- The front-end reads it with `content.GetValue<T>(alias)` → `StringExtensions.ToValue<T>`, so the stored format must
  deserialise to the `T` consumers request. Changing a value format is a breaking change (existing rows keep the old
  shape — e.g. ContentPicker has both single-string and JSON forms in the wild).
- Use invariant culture for numbers/dates in stored values (`NumericProperty` currently uses current culture — known issue).

## Settings round-trip

- Settings component receives `[Parameter] string? SettingsModel` and `[Parameter] EventCallback<string> ValueChanged`.
- Load: `Settings = this.GetPropertySettings();`  Save: `await this.SavePropertySettings(Settings);`
  (`ZauberCMS.Core/Extensions/PropertyExtensions.cs`). Saving closes the panel with the JSON, which is written to
  `PropertyType.Settings` and persisted **when the content type is saved**.
- Editor reads it: `var settings = Settings.FromJson<XSettingsModel>();` (string extension) or
  `this.GetPropertySettings<XSettingsModel>()`.
- Adding a settings property: give it a default so old JSON still deserialises. Never rename/remove one without a
  compat read (see `BlockListEditorSettingsModel` reading the old `Styleheets` key).

## Variants

- List editors (checkbox, dropdown, radio) share `Editors/Models/ListPropertyBaseComponent.cs` and
  `Settings/ListPropertySettings.razor`; data sources via `IDataListSource`.
- Rich-text toolbar extensions: `Editors/ToolbarItems/`.
- Block lists: [BlockListEditor.md](BlockListEditor.md).

## Availability

Editors appear in the "Add Property" panel (`Admin/StructureSection/Dialogs/ListContentTypeProperties.razor`) for
content types **and** roles. An editor in a separate class library appears only if that assembly is scanned
(`builder.AddZauberCms(assembly)`).

## Rich-text editor (ZauberCMS.RTE 3.x)

`ZauberCMSEditorProperty.razor` hosts `ZauberRichTextEditor`. Things that bite:

- **Toolbar items are registered, not auto-discovered by the editor.** `ZauberSetup` calls
  `AddZauberRte(o => o.AddToolbarItemsFromAssembly(...))` for every discovery assembly, so a public `IToolbarItem`
  / `ToolbarItemBase` in a scanned assembly still appears — but only if its id is also in the layout built in
  `BuildEditorSettings`. Items are created per editor instance (ctor DI works; instance fields are safe).
- Icons: use `IconSvg` (inline SVG, `stroke="currentColor"`, 24×24 viewBox). No icon font is shipped.
- Settings types are immutable records — build `EditorSettings` once (not per render) with object initialisers.
- The host page needs only `_content/ZauberCMS.RTE/css/zauber-editor.css`; the script is an ES module the component
  imports itself. Don't add script tags for it.
- **Content is sanitised in the browser with `HtmlPolicy` on load.** Tags outside the policy are stripped and lost on
  the next save; `iframe`, `script`, `style`, `object`, `embed`, form controls and `svg` are always stripped
  (ZA-134). `data-contentid` / `data-mediaid` survive. The server still sanitises separately.
- Theme is bound and starts light (the admin is light); `Auto` would follow the OS.
- Known 3.0.0 workarounds live in `InsertContentLinkItem` (ZA-136) — remove them when 3.1 is taken.

