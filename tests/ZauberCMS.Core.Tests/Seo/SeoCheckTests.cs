using System.Net;
using HtmlAgilityPack;
using ZauberCMS.Core.Seo.Checks;
using ZauberCMS.Core.Seo.Models;
using ContentItemModel = ZauberCMS.Core.Content.Models.Content;

namespace ZauberCMS.Core.Tests.Seo;

/// <summary>SEO checks can be stopped (ZA-085): the checker cancels a run when the editor leaves, runs the checks
/// again, presses Stop or the run takes too long.</summary>
public class SeoCheckTests
{
    private static HtmlDocument PageWithLinks(params string[] hrefs)
    {
        var document = new HtmlDocument();
        document.LoadHtml($"<html><body>{string.Join("", hrefs.Select(h => $"<a href=\"{h}\">link</a>"))}</body></html>");
        return document;
    }

    [Fact]
    public async Task ZA085_A_stopped_broken_links_check_stops_instead_of_reporting_every_link_broken()
    {
        var requests = 0;
        var check = new BrokenLinksCheck(new StubClientFactory(_ =>
        {
            requests++;
            return new HttpResponseMessage(HttpStatusCode.OK);
        }));
        using var stopped = new CancellationTokenSource();
        await stopped.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => check.Check("https://example.com/",
            PageWithLinks("/a", "/b"), new ContentItemModel(), stopped.Token));
        Assert.Equal(0, requests);
    }

    [Fact]
    public async Task ZA085_A_stopped_response_status_check_stops_instead_of_reporting_a_timeout()
    {
        var check = new ResponseStatusCheck(new StubClientFactory(_ => new HttpResponseMessage(HttpStatusCode.OK)));
        using var stopped = new CancellationTokenSource();
        await stopped.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => check.Check("https://example.com/",
            new HtmlDocument(), new ContentItemModel(), stopped.Token));
    }

    [Fact]
    public async Task ZA085_A_broken_links_check_that_is_not_stopped_still_reports_broken_links()
    {
        var check = new BrokenLinksCheck(new StubClientFactory(request =>
            new HttpResponseMessage(request.RequestUri!.AbsolutePath == "/gone" ? HttpStatusCode.NotFound : HttpStatusCode.OK)));

        var result = await check.Check("https://example.com/", PageWithLinks("/fine", "/gone"), new ContentItemModel(),
            TestContext.Current.CancellationToken);

        var broken = Assert.Single(result.Items);
        Assert.Contains("/gone", broken.DefaultMessage);
    }

    [Fact]
    public async Task ZA085_A_check_that_only_implements_the_old_method_runs_through_the_new_one()
    {
        ISeoCheck check = new OldStyleCheck();

        var result = await check.Check("https://example.com/", new HtmlDocument(), new ContentItemModel(),
            TestContext.Current.CancellationToken);

        Assert.Equal("Old style", result.Name);
    }

    private sealed class OldStyleCheck : ISeoCheck
    {
        public string Name => "Old style";
        public Task<SeoCheckResult> Check(string url, HtmlDocument document, ContentItemModel content) =>
            Task.FromResult(new SeoCheckResult(Name));
        public int SortOrder => 0;
    }

    private sealed class StubClientFactory(Func<HttpRequestMessage, HttpResponseMessage> respond) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(new StubHandler(respond));
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(respond(request));
        }
    }
}
