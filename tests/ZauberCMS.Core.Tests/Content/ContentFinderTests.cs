using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Content.Interfaces;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Content.Parameters;
using ZauberCMS.Core.Tests.Infrastructure;
using ContentItem = ZauberCMS.Core.Content.Models.Content;

namespace ZauberCMS.Core.Tests.Content;

/// <summary>
/// Roadmap D55: content finders decide which page an address shows when no page has its URL, in order, the first
/// that finds one winning, a failing one passed over, and only a live page served.
/// </summary>
public class ContentFinderTests : IAsyncLifetime
{
    private TestSite _site = null!;
    private ContentType _pageType = null!;

    public async ValueTask InitializeAsync()
    {
        _site = await TestSite.CreateAsync();
        _pageType = await _site.CreateContentTypeAsync("page");
    }

    public async ValueTask DisposeAsync() => await _site.DisposeAsync();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_finder_serves_a_page_for_an_address_no_page_has_with_its_route_values()
    {
        var product = await _site.CreateContentAsync(_pageType, "Product");
        using var script = ContentFinderScript.Start();
        // The first finder looks the page up through IContentService, which it gets by constructor injection
        script.First = context => context.Segments is ["products", var sku]
            ? new ContentFinderSearch("Product", new Dictionary<string, object?> { ["sku"] = sku })
            : null;

        var entry = await GetEntryAsync("/Products/ABC-123/");

        Assert.Equal(product.Id, entry.Content?.Id);
        Assert.Equal("abc-123", entry.RouteValues["sku"]);
        Assert.Equal(typeof(FirstTestFinder).FullName, entry.ContentFinder);
        // Loaded as the URL lookup loads a page for a view
        Assert.NotNull(entry.Content!.ContentType);
        Assert.False(entry.LanguageIsoCode is null or "");
        var asked = Assert.Single(script.Contexts);
        Assert.Equal("products/abc-123", asked.Slug);
        Assert.Equal("Products/ABC-123", asked.Path);
        Assert.Equal("http://localhost/Products/ABC-123/?ref=mail", asked.Url);
    }

    [Fact]
    public async Task When_no_finder_finds_a_page_the_address_has_none()
    {
        await _site.CreateContentAsync(_pageType, "Product");
        using var script = ContentFinderScript.Start();

        var entry = await GetEntryAsync("nothing-here");

        Assert.Null(entry.Content);
        Assert.Null(entry.ContentFinder);
        Assert.Equal([nameof(FirstTestFinder), nameof(SecondTestFinder)], script.Asked);
    }

    [Fact]
    public async Task Finders_are_not_asked_when_a_page_has_the_url()
    {
        var about = await _site.CreateContentAsync(_pageType, "About");
        var other = await _site.CreateContentAsync(_pageType, "Other");
        using var script = ContentFinderScript.Start();
        script.First = _ => new ContentFinderSearch(other.Id);

        var entry = await GetEntryAsync("about");

        Assert.Equal(about.Id, entry.Content?.Id);
        Assert.Null(entry.ContentFinder);
        Assert.Empty(script.Asked);
    }

    [Fact]
    public async Task Finders_run_in_sort_order_and_the_first_that_finds_a_page_wins()
    {
        var first = await _site.CreateContentAsync(_pageType, "First");
        var second = await _site.CreateContentAsync(_pageType, "Second");
        using var script = ContentFinderScript.Start();
        script.First = _ => new ContentFinderSearch(first.Id);
        script.Second = _ => new ContentFinderSearch(second.Id);

        var entry = await GetEntryAsync("somewhere");

        Assert.Equal(first.Id, entry.Content?.Id);
        Assert.Equal([nameof(FirstTestFinder)], script.Asked);

        // A finder that finds nothing leaves the address to the next
        script.First = _ => null;
        script.Asked.Clear();
        entry = await GetEntryAsync("somewhere");

        Assert.Equal(second.Id, entry.Content?.Id);
        Assert.Equal(typeof(SecondTestFinder).FullName, entry.ContentFinder);
        Assert.Equal([nameof(FirstTestFinder), nameof(SecondTestFinder)], script.Asked);
    }

    [Fact]
    public async Task A_finder_that_throws_or_cannot_be_created_is_passed_over()
    {
        var page = await _site.CreateContentAsync(_pageType, "Page");
        using var script = ContentFinderScript.Start();
        script.Throw = true;
        script.ThrowInConstructor = true;
        script.Second = _ => new ContentFinderSearch(page.Id);

        var entry = await GetEntryAsync("somewhere");

        Assert.Equal(page.Id, entry.Content?.Id);
        // The throwing finder runs first (SortOrder 0); the one whose constructor threw never ran
        Assert.Equal([nameof(ThrowingTestFinder), nameof(FirstTestFinder), nameof(SecondTestFinder)], script.Asked);
    }

    [Fact]
    public async Task A_page_that_is_not_live_is_never_served_through_a_finder()
    {
        var unpublished = await _site.CreateContentAsync(_pageType, "Unpublished");
        var recycled = await _site.CreateContentAsync(_pageType, "Recycled");
        var live = await _site.CreateContentAsync(_pageType, "Live");
        await _site.QueryDatabaseAsync(async db =>
        {
            (await db.Contents.SingleAsync(x => x.Id == unpublished.Id, Ct)).Published = false;
            return await db.SaveChangesAsync(Ct);
        });
        using (var scope = _site.CreateScope())
        {
            var result = await scope.ServiceProvider.GetRequiredService<IContentService>().DeleteContentAsync(
                new DeleteContentParameters { ContentId = recycled.Id, MoveToRecycleBin = true }, Ct);
            Assert.True(result.Success, TestSite.Describe(result.Messages));
        }

        using var script = ContentFinderScript.Start();
        script.First = _ => new ContentFinderSearch(unpublished.Id);
        script.Second = _ => new ContentFinderSearch(recycled.Id);

        Assert.Null((await GetEntryAsync("somewhere")).Content);
        Assert.Null((await GetEntryAsync("somewhere-else")).Content);

        // A finder naming an id no page has is passed over the same way, and the next live one is served
        script.First = _ => new ContentFinderSearch(Guid.NewGuid());
        script.Second = _ => new ContentFinderSearch(live.Id);
        Assert.Equal(live.Id, (await GetEntryAsync("somewhere")).Content?.Id);
    }

    [Fact]
    public async Task A_page_recycled_after_it_was_served_through_a_finder_is_not_served_again()
    {
        var page = await _site.CreateContentAsync(_pageType, "Page");
        using var script = ContentFinderScript.Start();
        script.First = _ => new ContentFinderSearch(page.Id);
        Assert.Equal(page.Id, (await GetEntryAsync("somewhere")).Content?.Id);

        using (var scope = _site.CreateScope())
        {
            var result = await scope.ServiceProvider.GetRequiredService<IContentService>().DeleteContentAsync(
                new DeleteContentParameters { ContentId = page.Id, MoveToRecycleBin = true }, Ct);
            Assert.True(result.Success, TestSite.Describe(result.Messages));
        }

        // The loaded page is cached under the Content prefix, which the delete clears
        Assert.Null((await GetEntryAsync("somewhere")).Content);
    }

    [Fact]
    public async Task ZA172_the_finders_a_lookup_builds_are_disposed_after_it()
    {
        var page = await _site.CreateContentAsync(_pageType, "Page");
        using var script = ContentFinderScript.Start();

        // No finder finds a page
        await GetEntryAsync("nothing-here");
        Assert.Equal(1, script.Disposed);
        Assert.Equal(1, script.AsyncDisposed);

        // One does, before the disposable ones are asked
        script.First = _ => new ContentFinderSearch(page.Id);
        Assert.Equal(page.Id, (await GetEntryAsync("somewhere")).Content?.Id);
        Assert.Equal(2, script.Disposed);
        Assert.Equal(2, script.AsyncDisposed);
    }

    [Fact]
    public async Task ZA173_a_finder_that_looks_its_own_address_up_does_not_start_the_finders_again()
    {
        await _site.CreateContentAsync(_pageType, "Home");
        using var script = ContentFinderScript.Start();
        script.Reenter = true;

        var entry = await GetEntryAsync("loop-me");

        Assert.Null(entry.Content);
        Assert.Equal(1, script.ReenterCalls);
    }

    [Fact]
    public async Task ZA173_a_lookup_from_inside_a_finder_still_finds_a_page_by_its_url()
    {
        var home = await _site.CreateContentAsync(_pageType, "Home");
        var about = await _site.CreateContentAsync(_pageType, "About", home.Id);
        using var script = ContentFinderScript.Start();
        script.Reenter = true;
        script.ReenterSlug = about.Url;

        var entry = await GetEntryAsync("old/about");

        Assert.Equal(about.Id, entry.Content?.Id);
        Assert.Equal(typeof(ReenteringTestFinder).FullName, entry.ContentFinder);
        Assert.Equal(1, script.ReenterCalls);
    }

    [Theory]
    [InlineData("not a culture")]
    [InlineData("fr_FR!")]
    public async Task ZA174_a_finder_language_that_is_not_a_culture_is_ignored(string languageIsoCode)
    {
        var page = await _site.CreateContentAsync(_pageType, "Page");
        using var script = ContentFinderScript.Start();
        script.First = _ => new ContentFinderSearch(page.Id) { LanguageIsoCode = languageIsoCode };
        var withoutFinderLanguage = await GetEntryWithLanguageAsync(page, null);

        var entry = await GetEntryAsync("somewhere");
        var again = await GetEntryAsync("somewhere");

        Assert.Equal(page.Id, entry.Content?.Id);
        // The page's own (here the site's default), which the culture middleware can use
        Assert.Equal(withoutFinderLanguage, entry.LanguageIsoCode);
        Assert.Equal(withoutFinderLanguage, again.LanguageIsoCode);
        Assert.NotNull(System.Globalization.CultureInfo.GetCultureInfo(entry.LanguageIsoCode!));
    }

    /// <summary>The language a finder serving <paramref name="page"/> with <paramref name="languageIsoCode"/> gives.</summary>
    private async Task<string?> GetEntryWithLanguageAsync(ContentItem page, string? languageIsoCode)
    {
        var script = ContentFinderScript.Current!;
        var first = script.First;
        script.First = _ => new ContentFinderSearch(page.Id) { LanguageIsoCode = languageIsoCode };
        var entry = await GetEntryAsync("somewhere-else");
        script.First = first;
        return entry.LanguageIsoCode;
    }

    [Fact]
    public async Task A_finder_can_set_the_language_the_page_is_shown_in()
    {
        var page = await _site.CreateContentAsync(_pageType, "Page");
        using var script = ContentFinderScript.Start();
        script.First = _ => new ContentFinderSearch(page.Id) { LanguageIsoCode = "fr-FR" };

        var entry = await GetEntryAsync("somewhere");

        Assert.Equal(page.Id, entry.Content?.Id);
        Assert.Equal("fr-FR", entry.LanguageIsoCode);
    }

    [Fact]
    public async Task A_finder_is_also_asked_for_the_home_page_when_the_site_has_no_root_page()
    {
        var page = await _site.CreateContentAsync(_pageType, "Page");
        await _site.QueryDatabaseAsync(async db =>
        {
            (await db.Contents.SingleAsync(x => x.Id == page.Id, Ct)).IsRootContent = false;
            return await db.SaveChangesAsync(Ct);
        });
        using var script = ContentFinderScript.Start();
        script.First = context => context.IsRootContent ? new ContentFinderSearch(page.Id) : null;

        using var scope = _site.CreateScope();
        var entry = await scope.ServiceProvider.GetRequiredService<IContentService>().GetContentFromRequestAsync(
            new GetContentFromRequestParameters { IsRootContent = true, Url = "http://localhost/" }, Ct);

        Assert.Equal(page.Id, entry.Content?.Id);
        Assert.Equal("", Assert.Single(script.Contexts).Slug);
    }

    private async Task<EntryModel> GetEntryAsync(string path)
    {
        using var scope = _site.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IContentService>().GetContentFromRequestAsync(
            new GetContentFromRequestParameters
            {
                Slug = path, Url = $"http://localhost/{path.TrimStart('/')}?ref=mail"
            }, Ct);
    }
}

/// <summary>What a test finder answers: a page by id, or the page with a name (looked up by the finder).</summary>
public sealed record ContentFinderSearch(Guid? ContentId, string? Name = null,
    IReadOnlyDictionary<string, object?>? RouteValues = null)
{
    public ContentFinderSearch(Guid contentId) : this((Guid?)contentId)
    {
    }

    public ContentFinderSearch(string name, IReadOnlyDictionary<string, object?> routeValues)
        : this(null, name, routeValues)
    {
    }

    public string? LanguageIsoCode { get; init; }
}

/// <summary>
/// What the test finders below do for the async flow of the test that called <see cref="Start"/>. They are
/// discovered for every test in the assembly, so without a script they find nothing and never throw. The throwing
/// ones are only listed in <see cref="Asked"/> when they throw.
/// </summary>
public sealed class ContentFinderScript : IDisposable
{
    private static readonly AsyncLocal<ContentFinderScript?> CurrentScript = new();

    public static ContentFinderScript? Current => CurrentScript.Value;

    public static ContentFinderScript Start() => CurrentScript.Value = new ContentFinderScript();

    public Func<ContentFinderContext, ContentFinderSearch?>? First { get; set; }
    public Func<ContentFinderContext, ContentFinderSearch?>? Second { get; set; }
    public bool Throw { get; set; }
    public bool ThrowInConstructor { get; set; }

    /// <summary>The finders asked, in order (type names).</summary>
    public List<string> Asked { get; } = [];

    /// <summary>The contexts the first finder was given.</summary>
    public List<ContentFinderContext> Contexts { get; } = [];

    /// <summary>ZA-173: the re-entering finder looks an address up through the service (<see cref="ReenterSlug"/>, else its own).</summary>
    public bool Reenter { get; set; }
    public string? ReenterSlug { get; set; }
    public int ReenterCalls { get; set; }

    /// <summary>ZA-172: how often the disposable finders were disposed.</summary>
    public int Disposed { get; set; }
    public int AsyncDisposed { get; set; }

    public void Dispose() => CurrentScript.Value = null;
}

/// <summary>ZA-173: looks an address up through IContentService from inside the finders (only when a script asks).</summary>
public class ReenteringTestFinder(IContentService contentService) : IContentFinder
{
    /// <summary>Stops the test, not the code under test, if the finders were entered again without end.</summary>
    private const int MaxDepth = 25;

    public int SortOrder => 5;

    public async Task<ContentFinderResult?> FindAsync(ContentFinderContext context, CancellationToken cancellationToken = default)
    {
        if (ContentFinderScript.Current is not { Reenter: true } script || script.ReenterCalls >= MaxDepth)
        {
            return null;
        }

        script.ReenterCalls++;
        var slug = script.ReenterSlug ?? context.Slug;
        var entry = await contentService.GetContentFromRequestAsync(
            new GetContentFromRequestParameters { Slug = slug, Url = $"http://localhost/{slug}" }, cancellationToken);
        return entry.Content == null ? null : new ContentFinderResult(entry.Content);
    }
}

/// <summary>ZA-172: a finder that owns something to dispose; it finds nothing.</summary>
public sealed class DisposableTestFinder : IContentFinder, IDisposable
{
    public int SortOrder => 30;

    public Task<ContentFinderResult?> FindAsync(ContentFinderContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult<ContentFinderResult?>(null);

    public void Dispose()
    {
        if (ContentFinderScript.Current is { } script)
        {
            script.Disposed++;
        }
    }
}

/// <summary>ZA-172: as <see cref="DisposableTestFinder"/>, disposed asynchronously.</summary>
public sealed class AsyncDisposableTestFinder : IContentFinder, IAsyncDisposable
{
    public int SortOrder => 31;

    public Task<ContentFinderResult?> FindAsync(ContentFinderContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult<ContentFinderResult?>(null);

    public ValueTask DisposeAsync()
    {
        if (ContentFinderScript.Current is { } script)
        {
            script.AsyncDisposed++;
        }

        return ValueTask.CompletedTask;
    }
}

public class ThrowingTestFinder : IContentFinder
{
    public int SortOrder => 0;

    public Task<ContentFinderResult?> FindAsync(ContentFinderContext context, CancellationToken cancellationToken = default)
    {
        if (ContentFinderScript.Current is not { Throw: true } script)
        {
            return Task.FromResult<ContentFinderResult?>(null);
        }

        script.Asked.Add(nameof(ThrowingTestFinder));
        throw new InvalidOperationException("A content finder failed");
    }
}

public class ConstructorThrowingTestFinder : IContentFinder
{
    public ConstructorThrowingTestFinder()
    {
        if (ContentFinderScript.Current?.ThrowInConstructor == true)
        {
            throw new InvalidOperationException("A content finder's constructor failed");
        }
    }

    public int SortOrder => 1;

    // Only asked when its constructor did not throw: it finds nothing
    public Task<ContentFinderResult?> FindAsync(ContentFinderContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult<ContentFinderResult?>(null);
}

public class FirstTestFinder(IContentService contentService) : IContentFinder
{
    public int SortOrder => 10;

    public async Task<ContentFinderResult?> FindAsync(ContentFinderContext context, CancellationToken cancellationToken = default)
    {
        if (ContentFinderScript.Current is not { } script)
        {
            return null;
        }

        script.Asked.Add(nameof(FirstTestFinder));
        script.Contexts.Add(context);
        return await TestFinders.ResultAsync(script.First?.Invoke(context), contentService, cancellationToken);
    }
}

public class SecondTestFinder(IContentService contentService) : IContentFinder
{
    public int SortOrder => 20;

    public async Task<ContentFinderResult?> FindAsync(ContentFinderContext context, CancellationToken cancellationToken = default)
    {
        if (ContentFinderScript.Current is not { } script)
        {
            return null;
        }

        script.Asked.Add(nameof(SecondTestFinder));
        return await TestFinders.ResultAsync(script.Second?.Invoke(context), contentService, cancellationToken);
    }
}

internal static class TestFinders
{
    public static async Task<ContentFinderResult?> ResultAsync(ContentFinderSearch? search,
        IContentService contentService, CancellationToken cancellationToken)
    {
        if (search == null)
        {
            return null;
        }

        Guid contentId;
        if (search.ContentId is { } id)
        {
            contentId = id;
        }
        else
        {
            // What a real finder does: find the page through the service
            var name = search.Name;
            var page = await contentService.QueryContentAsync(new QueryContentParameters
            {
                WhereClause = c => c.Name == name, AmountPerPage = 1
            }, cancellationToken);
            if (page.Items.FirstOrDefault() is not ContentItem found)
            {
                return null;
            }

            contentId = found.Id;
        }

        return new ContentFinderResult(contentId)
        {
            LanguageIsoCode = search.LanguageIsoCode,
            RouteValues = search.RouteValues ?? new Dictionary<string, object?>()
        };
    }
}
