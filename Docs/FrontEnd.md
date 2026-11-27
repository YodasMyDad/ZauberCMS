# Front-End (consumer sites)

Static SSR Razor components rendered by the catch-all route. Reference implementation: `ZauberCMS.Web/`
(`Pages/`, `Layouts/`, `ContentBlocks/`, `ContentBlockPreviews/`, `Custom/`, `_Imports.razor`).

## Request flow

1. `ZauberCMS.Routing/CatchAll.razor` (`@page "/{**slug}"`) — its assembly is added to routing **last** so it never
   shadows real routes (`ZauberSetup.cs` assembly list + `ExtensionManager.GetFilteredAssemblies`).
2. `ZauberCMS.Components/Pages/EntryPage.razor` reuses the content resolved by `CultureMiddleware`
   (`CultureMiddleware.GetEntryModel(HttpContext)`), falling back to `ContentService.GetContentFromRequestAsync`.
   The lookup normalises the slug with `ContentUrl.Normalize` (lower case, no slashes around it; empty = the root
   page), so `/About-Us/` finds `about-us` on every database. Only pages that are published, not in the recycle
   bin and not blocks are served — also as a domain root or an internal-redirect target — and the `Children` it
   loads follow the same rule. Blocks have no URL.
   `CultureMiddleware` looks content up only when the matched endpoint is the content catch-all (a route that is just
   `{**slug}`) or the Razor page at `/` (the home page has a page of its own, `Index.razor`; see
   `CultureMiddleware.IsContentEndpoint`). A site's own pages and endpoints, `/_blazor` and 404s are
   not looked up as the root page and run in the default request culture.
   A page's language is its domain's, its own, or its nearest ancestor's; with none of those it is
   `GlobalSettings.DefaultLanguage` (set by the installer), and only then `Zauber:AdminDefaultLanguage`.
3. Role-restricted content (`ContentRoles`) redirects to `Zauber:Identity:DefaultLoginRedirectUrl` unless the user has
   a role (admins always pass); the page's view is not rendered (ZA-110).
4. The view is looked up by `Content.ViewComponent` == **view type `FullName`** (namespace + class) among
   `IContentView` implementations, then rendered inside `<LayoutView>` using the view's `@layout`.
   Fallbacks: `Zauber:NotFoundComponent`, `Zauber:StarterComponent`, `Zauber:MissingView` (also full type names).
5. No page at the address (and the site has content): `EntryPage` renders `Zauber:NotFoundComponent` in its layout
   with status 404 (D41). The status is set by ZauberCMS, so a site's own not-found view does not set it. It is set
   as the response starts (`HttpContext.SetStatusCodeWhenResponseStarts`), because .NET 10 sends a static render
   that ends at 404 **without its HTML**, leaving the body to status-code pages: a view or page that sets
   `HttpContext.Response.StatusCode = 404` itself is sent empty. A site page that shows a not-found view of its own
   calls `SetStatusCodeWhenResponseStarts(404)` instead. Only a response still at 200 is changed, so a redirect or
   the error page that comes later keeps its status. The registration page, when it is off, answers the same way.
   Inside an admin circuit there is no response; the view is shown.

## Errors (Roadmap D33)

Outside Development, a request that throws is shown `Zauber:ErrorPagePath` (default `/error`) with status 500,
through ASP.NET's exception handler, which `AddZauberCms` adds first in the pipeline. Make a content page at
`/error` to give the site its own error page (a published page with that URL; it renders with its view and layout),
or point the setting at a page or endpoint of the site. When there is nothing at that address, or it fails as well,
`ErrorPageMiddleware` sends a plain built-in page ("Something went wrong" and the request id, which is in the log
next to the exception). Keep the error page simple: if it fails for the same reason as the page (the database is
down), the built-in page is what visitors see. Empty `ErrorPagePath` adds no handler, for a site that calls
`UseExceptionHandler` itself. Development keeps the developer exception page.
The error page is requested as a GET whatever the failed request was (a failed form post is not posted to it), and
neither the installer nor a redirect rule can replace it. With no published page at the address, `EntryPage` throws
rather than render a 404, so the built-in page is sent; that shows in the log as a second error.

`/admin`, `/account`, `/api` and `/_blazor` are ZauberCMS's own, matched on whole path segments (`SitePaths.IsSystemPath`):
`/administration`, `/accounting` or `/api-docs` are ordinary pages of the site, with redirects and culture (ZA-095).

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
| `NavigationItems`, `Url()` | Menus and links (`Url()` is the stored form, `blog`; `ContentUrl.SiteRelative(content)` gives the link, `/blog`) |

Use `Cached = true` on every `QueryContentAsync`/`GetContentAsync` on the request path.

## Blocks

- Front-end block component: implements `IContentBlockView`, `ContentTypeAlias` = the element type alias
  (`ZauberCMS.Web/ContentBlocks/FAQBlock.razor`). Render with `<RenderBlock Content="@block" TView="IContentBlockView"/>`.
- Admin preview is a **separate** component implementing `IContentBlockPreview` (`ZauberCMS.Web/ContentBlockPreviews/`).
  It is rendered to static HTML (`HtmlRenderer`) and shown inert: no clicks, JS, `OnAfterRender` or `@rendermode`.
  To show nested blocks with their unsaved edits, call `GetBlocks(alias, ContentService, includePendingChanges: true)`.
- `IContentBlockView.RenderMode` is ignored by `RenderBlock` (only the unwired MVC view-component path reads it).
- Rich text from content is HTML: render through `IHtmlSanitizerService` rather than a raw `MarkupString`
  (`@Sanitizer.SanitizeToMarkup(Content.GetValue<string>("Text"))`). The default sanitiser also turns the
  editor's video placeholders into players: `<figure class="zauber-embed" data-embed="youtube"><iframe …>`,
  560 × 315 by attribute. Make them responsive in the site's CSS, for example
  `.zauber-embed iframe { width: 100%; height: auto; aspect-ratio: 16 / 9; }`. A site with its own
  `IHtmlSanitizerService` must call `IRichTextEmbedService.Expand` on the sanitised HTML itself, or videos do
  not show.
- Details: [BlockListEditor.md](BlockListEditor.md).

## Interactivity

Opt in per component: `@rendermode InteractiveServer` on the child (e.g. `ZauberCMS.Web/Custom/ContactForm.razor`).
The component runs in the language of the page it is on (`ContentCultureCircuitHandler`), like the page itself.
With enhanced navigation, a component that is on both pages is kept, not started again, and Blazor does not
re-render it: it still shows what it rendered on the previous page until it next updates (then in the new page's
language). A per-page `@key` (for example `@key="Content?.Id"`) should make it start fresh on each page (Blazor matches kept components by type and key; not tried here).
SSR forms use `EditForm` + `FormName` + `[SupplyParameterFromForm]`. Never Radzen on the front-end.

## Discovery of site code

The host's entry assembly is scanned automatically. Views/blocks/plugins in other class libraries must be passed to
`builder.AddZauberCms(typeof(SomeType).Assembly)`.
