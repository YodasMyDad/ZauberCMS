using HtmlAgilityPack;
using Microsoft.Extensions.Localization;
using ZauberCMS.Core.Localization;
using ZauberCMS.Core.Seo.Models;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Seo.Checks;

public class ResponseStatusCheck(IHttpClientFactory httpClientFactory, IStringLocalizer<ZauberText> text) : ISeoCheck
{
    public string Name => text["seo_responseStatusCheck"];

    public Task<SeoCheckResult> Check(string url, HtmlDocument document, Content.Models.Content content) =>
        Check(url, document, content, CancellationToken.None);

    public async Task<SeoCheckResult> Check(string url, HtmlDocument document, Content.Models.Content content,
        CancellationToken cancellationToken)
    {
        var result = new SeoCheckResult(Name);
        var seoItem = new SeoCheckResultItem();

        try
        {
            // The SEO client refuses private addresses and has a timeout (see SeoHttp).
            var httpClient = httpClientFactory.CreateClient(SeoHttp.ClientName);
            // We grab a random user agent so we are not using the same one each time
            httpClient.DefaultRequestHeaders.Add("User-Agent", Constants.UserAgents.OrderBy(x => Guid.NewGuid()).FirstOrDefault());
            // Perform the HTTP GET request
            using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            var statusCode = (int)response.StatusCode;

            // Handle status codes
            if (statusCode is 200)
            {
                seoItem.Status = AlertType.Success;
                seoItem.DefaultMessage = text["seo_statusOk"];
            }
            else if (statusCode is 301 or 302)
            {
                seoItem.Status = AlertType.Warning;
                seoItem.DefaultMessage = text["seo_statusRedirect", statusCode];
            }
            else
            {
                seoItem.Status = AlertType.Error;
                seoItem.DefaultMessage = text["seo_statusOther", statusCode];
            }
        }
        catch (HttpRequestException ex)
        {
            // Handle network or HTTP request-specific errors
            seoItem.Status = AlertType.Error;
            seoItem.DefaultMessage = text["seo_statusNetworkError", ex.Message];
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The run was stopped
            throw;
        }
        catch (TaskCanceledException)
        {
            seoItem.Status = AlertType.Error;
            seoItem.DefaultMessage = text["seo_pageTimedOut"];
        }
        catch (Exception ex)
        {
            // Handle general errors
            seoItem.Status = AlertType.Error;
            seoItem.DefaultMessage = text["seo_unexpectedError", ex.Message];
        }

        result.Items.Add(seoItem);

        return result;
    }

    public int SortOrder => 5;
}