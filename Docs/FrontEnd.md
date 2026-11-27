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
   A request is on one domain at most (`DomainMatching`): of the domains whose host (and path, on whole segments)
   it matches, the one with the longest path, so `example.com/fr/…` is on `example.com/fr`, not `example.com`.
   A domain's scheme, trailing dot and slashes are ignored; one with a port (`example.com:8080`) matches only that
   port, one without matches any port. SEO redirects use the same rule for a redirect's domain.
   A domain's path is matched against the path below the site's path base (`HttpRequest.Path`, not `PathBase`), in
   the middlewares, in `EntryPage` and in a circuit alike (ZA-190, D64): under `/site`, `example.com/fr` covers
   `/site/fr/…`. Code that looks a page up from a component passes `NavigationManager.GetContentRequestUrl()` as
   `GetContentFromRequestParameters.Url`, which leaves the path base out.
3. Role-restricted content (`ContentRoles`) redirects to `Zauber:Identity:DefaultLoginRedirectUrl` unless the user has
   a role (admins always pass); the page's view is not rendered (ZA-110).
4. The view is looked up by `Content.ViewComponent` == **view type `FullName`** (namespace + class) among
   `IContentView` implementations, then rendered inside `<LayoutView>` using the view's `@layout`.
   Fallbacks: `Zauber:NotFoundComponent`, `Zauber:StarterComponent`, `Zauber:MissingView` (also full type names).
5. No live page has the URL: the site's content finders (`IContentFinder`, below) are asked, in order, which page
   the address shows. The first that names a live page wins; it is served like a page found by its URL.
6. No page at the address (and the site has content): `EntryPage` renders `Zauber:NotFoundComponent` in its layout
   with status 404 (D41). The status is set by ZauberCMS, so a site's own not-found view does not set it. It is set
   as the response starts (`HttpContext.SetStatusCodeWhenResponseStarts`), because .NET 10 sends a static render
   that ends at 404 **without its HTML**, leaving the body to status-code pages: a view or page that sets
   `HttpContext.Response.StatusCode = 404` itself is sent empty. A site page that shows a not-found view of its own
   calls `SetStatusCodeWhenResponseStarts(404)` instead. Only a response still at 200 is changed, so a redirect or
   the error page that comes later keeps its status. The registration page, when it is off, answers the same way.
   Inside an admin circuit there is no response; the view is shown.

## Content finders (Roadmap D55)

A content finder decides which page an address shows when no page has its URL: `/products/{sku}` showing one
product page for every SKU, an old URL scheme served without redirects, a page found by a code in the address.
It is ZauberCMS's form of Umbraco's `IContentFinder`; Umbraco's last-chance finder is `Zauber:NotFoundComponent`.

- Implement `IContentFinder` (`ZauberCMS.Core.Content.Interfaces`) in a public class in a scanned assembly. It is
  discovered (no registration) and built for each lookup with constructor injection.
- Finders run in `SortOrder` order, lowest first (the same value: by type full name), only after the URL lookup has
  found no live page, for the home page too. The first one that returns a result wins; returning null asks the
  next, and in the end the not-found page answers with 404. They run inside `IContentService.GetContentFromRequestAsync`,
  so the page request, `EntryPage`, the page's language for interactive components and a site's own calls all get
  the same answer. A lookup made from inside a finder (the finder, or code it calls, calling
  `GetContentFromRequestAsync`) does the URL lookup only and does not ask the finders again, so a finder that maps
  an old address to a new one can look the new one up without starting itself over.
- A result names the page by id (`new ContentFinderResult(page)` or `(page.Id)`). ZauberCMS loads that page again
  as the URL lookup would, and serves it only when it is published, not in the recycle bin and not a block (its
  internal redirect is followed, role restrictions apply as usual); otherwise the next finder is asked. So a finder
  cannot show an unpublished page, and unpublishing the page takes every address it served off the site.
- `RouteValues` go to the view through `EntryModel.RouteValues`; `EntryPage` cascades the `EntryModel` to the view
  and its layout. `LanguageIsoCode` sets the language the page is shown in; it wins over the domain's and the
  page's own. Null gives the language of the domain the address is on, else the page's own or its nearest
  ancestor's, else the site default. A value that is not a culture name is ignored (logged once as a warning).
- A finder that throws, or whose constructor throws, is logged as an error and the next one is asked: one broken
  plugin does not take the site's not-found page down. Every finder is built for each lookup and disposed after
  it when it is `IDisposable` or `IAsyncDisposable`; the services it injects belong to the request's scope.
- ZauberCMS caches the page it loads for a finder (cleared by content saves, like a page found by its URL) but not
  the finder's own answer: a finder runs on every request for an address no page has, bots' 404s included. Keep
  it cheap, return null fast for addresses it does not handle, and cache an expensive search itself
  (`ICacheService`).
- `ContentFinderContext` gives the address as asked for (`Url`, with its query string; null when code looked a slug
  up), `Slug` in the stored URL form (lower case, no slashes around it, empty for the home page) and its
  `Segments`, `Path` in the case it was asked for (for a value whose case matters), and the culture `Domain` it
  matched. A finder that needs the request itself injects `IHttpContextAccessor` (there
  is no request inside a circuit).

```csharp
using ZauberCMS.Core.Content.Interfaces;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Content.Parameters;

namespace MySite.Routing;

/// <summary>/products/{sku} shows the site's product page for every SKU the catalogue has.</summary>
public class ProductContentFinder(IContentService contentService, IProductCatalogue catalogue) : IContentFinder
{
    public int SortOrder => 100;

    public async Task<ContentFinderResult?> FindAsync(ContentFinderContext context,
        CancellationToken cancellationToken = default)
    {
        // Only addresses of this shape; anything else is left to the next finder at once
        if (context.Segments is not ["products", var sku] || !await catalogue.ExistsAsync(sku, cancellationToken))
        {
            return null; // an unknown SKU is a 404, like any address no page has
        }

        var page = await contentService.GetContentAsync(
            new GetContentParameters { ContentTypeAlias = "productPage", Cached = true }, cancellationToken);
        return page == null
            ? null
            : new ContentFinderResult(page) { RouteValues = new Dictionary<string, object?> { ["sku"] = sku } };
    }
}
```

The product page's view reads the SKU from the cascaded `EntryModel`:

```razor
@implements IContentView
@layout MainLayout

<h1>@Product?.Name</h1>

@code {
    [Parameter] public Content? Content { get; set; }
    [Parameter] public Dictionary<string, string>? LanguageKeys { get; set; }
    [CascadingParameter] public EntryModel? Entry { get; set; }

    [Inject] public IProductCatalogue Catalogue { get; set; } = null!;
    private Product? Product { get; set; }

    protected override async Task OnParametersSetAsync()
    {
        if (Entry?.RouteValues.GetValueOrDefault("sku") is string sku)
        {
            Product = await Catalogue.GetAsync(sku);
        }
    }
}
```

The product page keeps its own URL too (`/product-page`, say), where the view is shown without a SKU: the view can
see that from `Entry?.ContentFinder` (null there) and answer 404 itself
(`HttpContext.SetStatusCodeWhenResponseStarts(404)`), and the page can be hidden from navigation and the sitemap.
`IProductCatalogue` stands for the site's own service.

`RenderSeoTags` gives a page served through a finder the address asked for as its canonical URL and `og:url`
(scheme, host and path, without the query string: `/products/abc-123`), not the page's own URL, so each SKU
address is its own page to search engines and the canonical never points at the base page that answers 404. The
path is taken as it was asked for, so where several spellings reach one page (`/products/ABC-123` and
`/products/abc-123`) each is its own canonical: send the others on to one spelling from the view, or render the
tags in the view itself.

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
| `NavigationItems`, `Content.Url` | Menus and links (`Content.Url` is the stored form, `blog`; `ContentUrl.SiteRelative(content)` gives the link, `/blog`; the `Url()` extension is obsolete, D57) |

Use `Cached = true` on every `QueryContentAsync`/`GetContentAsync` on the request path.

## Blocks

- Front-end block component: implements `IContentBlockView`, `ContentTypeAlias` = the element type alias
  (`ZauberCMS.Web/ContentBlocks/FAQBlock.razor`). Render with `<RenderBlock Content="@block" TView="IContentBlockView"/>`.
- Admin preview is a **separate** component implementing `IContentBlockPreview` (`ZauberCMS.Web/ContentBlockPreviews/`).
  It is rendered to static HTML (`HtmlRenderer`) and shown inert: no clicks, JS, `OnAfterRender` or `@rendermode`.
  To show nested blocks with their unsaved edits, call `GetBlocks(alias, ContentService, includePendingChanges: true)`.
- A block that needs interactivity sets its own `@rendermode`; `IContentBlockView` has no render-mode member (its
  MVC-typed `RenderMode` went with the MVC rendering path, Roadmap Phase 11e). ZauberCMS renders no MVC views and
  registers controllers with MVC's view features but no Razor view engine (`AddControllers` and
  `AddMvcCore().AddViews()`); a site that wants Razor views (`.cshtml`) calls `AddControllersWithViews()` itself.
  A site's or plugin's controller can use `[ValidateAntiForgeryToken]`, `[AutoValidateAntiforgeryToken]` and
  `[IgnoreAntiforgeryToken]` (D60): an action that checks answers 400 without a valid token.
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

**Custom components** (`ICustomContentComponent`): components an editor puts on a page with the "Custom Component
Picker" property editor; the view renders the chosen ones with
`<RenderCustomContent Content="@Content" CustomComponents="@Content.GetValue<List<string>>("alias")" />`
(`ZauberCMS.Web/Pages/ContactView.razor`, `ZauberCMS.Web/Custom/ContactForm.razor`). A component is given
`[Parameter] public Guid ContentId`, the id of the page it is on, not the page: the parameters of a component with
`@rendermode` are serialised into the HTML, and the whole page graph does not belong there (ZA-126, D58). One that
needs the page loads it: `await ContentService.GetContentAsync(new GetContentParameters { Id = ContentId, Cached = true })`
(only a published page that is not in the recycle bin is returned by default).

## Discovery of site code

The host's entry assembly is scanned automatically. Views/blocks/plugins in other class libraries must be passed to
`builder.AddZauberCms(typeof(SomeType).Assembly)`.
