# Front-End (consumer sites)

Static SSR Razor components rendered by the catch-all route. Reference implementation: `ZauberCMS.Web/`
(`Pages/`, `Layouts/`, `ContentBlocks/`, `ContentBlockPreviews/`, `Custom/`, `_Imports.razor`).

## Request flow

1. `ZauberCMS.Routing/CatchAll.razor` (`@page "/{**slug}"`) — its assembly is added to routing **last** so it never
   shadows real routes (`ZauberSetup.cs` assembly list + `ExtensionManager.GetFilteredAssemblies`).
2. `ZauberCMS.Components/Pages/EntryPage.razor` reuses the content resolved by `CultureMiddleware`
   (`CultureMiddleware.GetEntryModel(HttpContext)`), falling back to `ContentService.GetContentFromRequestAsync`.
3. Role-restricted content (`ContentRoles`) redirects to `Zauber:Identity:DefaultLoginRedirectUrl` unless the user has
   a role (admins always pass).
4. The view is looked up by `Content.ViewComponent` == **view type `FullName`** (namespace + class) among
   `IContentView` implementations, then rendered inside `<LayoutView>` using the view's `@layout`.
   Fallbacks: `Zauber:NotFoundComponent`, `Zauber:StarterComponent`, `Zauber:MissingView` (also full type names).

## Writing a view

```razor
@implements IContentView
@layout MainLayout

<SectionContent SectionId="MainLayout.Sections.PageHeadContent">
    <RenderSeoTags SeoAlias="SEO" Content="@Content" />
</SectionContent>

<h1>@Content!.GetValue<string>("Heading")</h1>
@foreach (var block in _blocks) { <RenderBlock Content="@block" TView="IContentBlockView" /> }

@code {
    [Parameter] public Content? Content { get; set; }
    [Parameter] public Dictionary<string, string>? LanguageKeys { get; set; }
    private IEnumerable<Content> _blocks = [];

    protected override async Task OnInitializedAsync()
        => _blocks = await Content!.GetBlocks("ContentBlocks", ContentService);
}
```

- **Every view must declare `@layout`** — `LayoutResolverService` throws otherwise.
- Renaming a view class or its namespace orphans content using it; update the content types in admin.
- Copy `ZauberCMS.Web/Pages/HomeView.razor` (data loading, SEO head, blocks).
- Layouts don't receive `Content`; load what they need (e.g. `ZauberCMS.Web/Layouts/MainLayout.razor` loads the
  `Website` root with `GetContentAsync { ContentTypeAlias = "Website", Cached = true }`) and define `SectionOutlet`s.
- Services come from `_Imports.razor` `@inject`s. The template scaffold ships no `_Imports.razor`, Pages or Layouts —
  the site creates them.

## Content helpers (`ZauberCMS.Core/Extensions/ContentExtensions.cs`)

| Helper | Use |
| --- | --- |
| `GetValue<T>(alias)` | Property value converted via `ToValue<T>` (JSON for complex types) |
| `GetBlocks(alias, contentService)` | Block-list items, one level, in order; unpublished excluded |
| `GetMediaItems` / `GetMedia` | Media picker values |
| `GetContentItems` / `GetContent` | Content picker values |
| `GetUsers` / `GetUser` | User picker values |
| `NavigationItems`, `Url()` | Menus and links |

Use `Cached = true` on every `QueryContentAsync`/`GetContentAsync` on the request path.

## Blocks

- Front-end block component: implements `IContentBlockView`, `ContentTypeAlias` = the element type alias
  (`ZauberCMS.Web/ContentBlocks/FAQBlock.razor`). Render with `<RenderBlock Content="@block" TView="IContentBlockView"/>`.
- Admin preview is a **separate** component implementing `IContentBlockPreview` (`ZauberCMS.Web/ContentBlockPreviews/`).
- `IContentBlockView.RenderMode` is ignored by `RenderBlock` (only the unwired MVC view-component path reads it).
- Rich text from content is HTML: render through `IHtmlSanitizerService` rather than a raw `MarkupString`.
- Details: [BlockListEditor.md](BlockListEditor.md).

## Interactivity

Opt in per component: `@rendermode InteractiveServer` on the child (e.g. `ZauberCMS.Web/Custom/ContactForm.razor`).
SSR forms use `EditForm` + `FormName` + `[SupplyParameterFromForm]`. Never Radzen on the front-end.

## Discovery of site code

The host's entry assembly is scanned automatically. Views/blocks/plugins in other class libraries must be passed to
`builder.AddZauberCms(typeof(SomeType).Assembly)`.
