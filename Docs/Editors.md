# Property Editors

Paths relative to `ZauberCMS.Components/` unless prefixed. Reference set to copy: `Editors/NumericProperty.razor` +
`Editors/Settings/NumericPropertySettings.razor` + `Editors/Models/NumericPropertySettingsModel.cs`. For an editor
that opens a dialog: `Editors/EditorNotesProperty.razor` + `Editors/Dialogs/EditorNotesDialog.razor`.

`IContentProperty` takes the modal's `IModalService`, so it is in the ZauberCMS.Components assembly (namespace
`ZauberCMS.Core.Content.Interfaces`, Roadmap D51): a class library with editors references `ZauberCMS.Components`
(or the `ZauberCMS` package), not only `ZauberCMS.Core`. `IContentPropertySettings<T>` stays in Core.

## Files

| File | Implements |
| --- | --- |
| `Editors/XProperty.razor` | `IContentProperty` (`ZauberCMS.Components/Core/Content/Interfaces/IContentProperty.cs`; namespace `ZauberCMS.Core.Content.Interfaces`, assembly ZauberCMS.Components) |
| `Editors/Models/XSettingsModel.cs` (optional) | plain class, public get/set, parameterless ctor |
| `Editors/Settings/XSettings.razor` (optional) | `IContentPropertySettings<XSettingsModel>` |

## Editor contract

- **Metadata** (`Name`, `Alias`, `Description`, `Icon`, `SettingsComponent`, `Scripts`, `Styles`, `FullWidth`) is read
  from an instance built without DI — plain initialisers only.
- `Alias` must be globally unique (the "Add Property" picker resolves by it). Third-party editors must not use the
  `ZauberCMS.` prefix.
- Required `[Parameter]`s — `DynamicContentProperty` always passes them and throws if one is missing:
  `string? Value`, `EventCallback<string> ValueChanged`, `string? Settings`, `Content? Content`.
- Optional: `[Parameter] string? PropertyAlias`. An editor that holds blocks (nested content) takes the cascading
  `ContentEditScope` and registers an `INestedContentReferenceProvider` for its alias
  (see [BlockListEditor.md](BlockListEditor.md) §7). `ISupportsBlockListChanges` is obsolete and never called.
- An editor never writes to the database: it only raises `ValueChanged`, and the page save writes what the value
  implies, in its transaction. An editor whose value holds tags registers an `ITagReferenceProvider` for its alias, and
  the save writes the item's tag links from the published value (the built-in `ZauberCMS.Tags` editor does this;
  [Services.md](Services.md) "Deletes", Tags).
- `[CascadingParameter] IModalService? ModalService` for dialogs (`ModalService.OpenSidePanel<T>(…)`, then `await modal.Result`).
- `Content` is **null** when the editor renders for User/Role properties — handle it.
- `SettingsComponent = null` → admin shows "No settings available".
- `Scripts`/`Styles` are emitted on every `/admin` page; use `_content/{AssemblyName}/…` paths.

## Persistence

- A property type stores `Component = editorType.FullName` and `ComponentAlias = Alias`. **Renaming the class or
  namespace orphans every existing property** using it (the sitemap also hard-codes
  `ZauberCMS.Components.Editors.SeoProperty`).
- The value is always a **string**; complex values are JSON. Emit with `await ValueChanged.InvokeAsync(value)`; don't
  assign your own `Value` parameter (the parent owns it, and its next render overwrites what you wrote).
- The front-end reads it with `content.GetValue<T>(alias)` → `StringExtensions.ToValue<T>`, so the stored format must
  deserialise to the `T` consumers request. Changing a value format is a breaking change: existing rows keep the old
  shape, so the read side must take both (as the pickers do, Roadmap D30).
- **Pickers** (content, media, users) store a JSON array of ids in the order the editor chose, even for one item:
  write with `ids.ToPickerValue()`, read with `value.ToPickerIds()` (`Core/Extensions/PickerValueExtensions.cs`),
  which also reads a bare id from before D30. Query the items with `AmountPerPage = ids.Count` (the default is 10)
  and put them back in order with `InPickerOrder`; queries return their own order. Save after a drag, not only after
  an add or remove. `GetContentItems` / `GetMediaItems` / `GetUsers` do this for views.
- **API keys** are stored by name, never by value (ZA-214, Roadmap D73): the API Key Picker and TinyMCE's settings
  keep the key's name, and the value is read on the server where it is used, with
  `IApiKeyService.GetPickedApiKeyAsync(value)` (a value stored before v5 is the key itself and comes back as it is;
  the picker shows it as its key's name and stores the name from the next save) or `GetApiKeyAsync(name)` for a fixed
  name (Google Maps reads `GoogleMaps`). Keys come from `Zauber:ApiKeys` in configuration, which wins, and Global
  Settings. Never render a key into the admin. A key that a script in the browser needs (TinyMCE's cloud key, the
  Maps JavaScript key) reaches the browser anyway: restrict it to the site's domains with its provider.
- **Numbers and dates** are stored in the invariant culture (`1.5`, `2026-11-26T14:30:00`, format `"s"`); the admin
  runs in `AdminDefaultLanguage` and a page in its own language, so a culture-formatted value reads differently on the
  site. `ToValue<double|decimal|DateTime>` reads the invariant form first and the current culture's as a fallback,
  for values written before D31.
- An editor that reads files with `InputFile` must copy each file when it is chosen (`StagedBrowserFile`, used by
  `MultipleMediaUpload`): Blazor's `IBrowserFile` can't be read after the input's next change.

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
  `Settings/ListPropertySettings.razor`; data sources via `IDataListSource`. The editors call `GetItemsAsync`
  (default: `GetItems`); a source that queries a database implements `GetItemsAsync` (Phase 10).
- Rich-text toolbar extensions: `Editors/ToolbarItems/`.
- Block lists: [BlockListEditor.md](BlockListEditor.md).

## Availability

Editors appear in the "Add Property" panel (`Admin/StructureSection/Dialogs/ListContentTypeProperties.razor`) for
content types **and** roles. An editor in a separate class library appears only if that assembly is scanned
(`builder.AddZauberCms(assembly)`).

## Rich-text editor (ZauberCMS.RTE)

`ZauberCMSEditorProperty.razor` hosts `ZauberRichTextEditor`. Things that bite:

- **Toolbar items are registered, not auto-discovered by the editor.** `ZauberSetup` calls
  `AddZauberRte(o => o.AddToolbarItemsFromAssembly(...))` for every discovery assembly, so a public `IToolbarItem`
  / `ToolbarItemBase` in a scanned assembly still appears — but only if its id is also in the layout built in
  `BuildEditorSettings`. Items are created per editor instance (ctor DI works; instance fields are safe).
- Icons: use `IconSvg` (inline SVG, `stroke="currentColor"`, 24×24 viewBox). No icon font is shipped.
- Settings types are immutable records — build `EditorSettings` once (not per render) with object initialisers.
- The editor loads its own stylesheet and script; the host page needs no `<link>` or script tag for it. Admin
  CSS cannot restyle the editor through bare element selectors — use the `--rte-*` tokens or class selectors
  such as `.zauber-rte .rte-content p`.
- **Content is sanitised in the browser with `HtmlPolicy` on load.** Tags outside the policy are stripped and lost on
  the next save; `script`, `style`, `object`, `embed`, form controls and `svg` are always stripped.
  `data-contentid` / `data-mediaid` survive. The server still sanitises separately.
- **Content links follow their page.** When a value opens, a link with `data-contentid` whose href no longer
  matches the page's URL is rewritten to `ContentUrl.SiteRelative` (`/blog`; a `?query` or `#fragment` is kept);
  both sides are compared through `ContentUrl.Normalize`, so `/blog` and `blog` are the same. A link to an
  unpublished or recycled page is kept; only a link to a page that no longer exists becomes plain text. Editing a content link on
  the Manual tab keeps its `data-contentid` while the URL still points at that page.
- **Video embeds are placeholders, not iframes.** The `embed` toolbar item stores
  `<figure data-embed="youtube" data-embed-id="…" data-embed-url="…">`. An existing `<iframe>` from an allowed
  provider is converted to a placeholder when the editor loads the value; an iframe from anywhere else is
  removed with a warning toast. The provider list comes from `IRichTextEmbedService.Providers` (Core; default
  YouTube and Vimeo), and the same service turns placeholders into players when the site renders the value
  (see FrontEnd.md). A site adds a provider by registering its own `IRichTextEmbedService` — usually a subclass
  of `DefaultRichTextEmbedService` overriding `Providers` — after `AddZauberCms`.
- Theme is bound and starts light (the admin is light); `Auto` would follow the OS.
- `InsertContentLinkItem` selects the whole existing link *before* it opens the dialog, so the saved selection
  covers it and the edited link replaces the old one.

