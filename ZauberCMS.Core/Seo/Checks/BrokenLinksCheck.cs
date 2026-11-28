using HtmlAgilityPack;
using Microsoft.Extensions.Localization;
using ZauberCMS.Core.Localization;
using ZauberCMS.Core.Seo.Models;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Seo.Checks;

public class BrokenLinksCheck(IHttpClientFactory httpClientFactory, IStringLocalizer<ZauberText> text) : ISeoCheck
{
    public string Name => text["seo_brokenLinksCheck"];

    public Task<SeoCheckResult> Check(string url, HtmlDocument document, Content.Models.Content content) =>
        Check(url, document, content, CancellationToken.None);

    public async Task<SeoCheckResult> Check(string url, HtmlDocument document, Content.Models.Content content,
        CancellationToken cancellationToken)
    {
        var result = new SeoCheckResult(Name);
        var seoItems = new List<SeoCheckResultItem>();

        // Exclude nav, header, footer, and other non-content sections
        var contentNode = document.DocumentNode.SelectSingleNode("//body");
        if (contentNode == null)
        {
            result.Items.Add(new SeoCheckResultItem
            {
                Status = AlertType.Warning,
                DefaultMessage = text["seo_noBodyContent"].Value
            });
            return result;
        }

        //var excludedTags = new HashSet<string> { "nav", "header", "footer", "aside" };
        var excludedTags = new HashSet<string>();
        var links = contentNode.Descendants("a")
            .Where(a => a.Attributes["href"] != null &&
                        !string.IsNullOrEmpty(a.GetAttributeValue("href", "").Trim()) &&
                        !a.GetAttributeValue("href", "").StartsWith('#') &&
                        !excludedTags.Contains(a.Ancestors().FirstOrDefault()?.Name ?? ""))
            .Select(a => a.GetAttributeValue("href", "").Trim())
            .Distinct()
            .ToList();

        if (links.Count == 0)
        {
            result.Items.Add(new SeoCheckResultItem
            {
                Status = AlertType.Warning,
                DefaultMessage = text["seo_noContentLinks"].Value
            });
            return result;
        }

        // The SEO client refuses private addresses and has a timeout (see SeoHttp).
        var httpClient = httpClientFactory.CreateClient(SeoHttp.ClientName);
        
        // We grab a random user agent so we are not using the same one each time
        httpClient.DefaultRequestHeaders.Add("User-Agent", Constants.UserAgents.OrderBy(x => Guid.NewGuid()).FirstOrDefault());
        
        foreach (var link in links.Take(50))
        {
            var seoItem = new SeoCheckResultItem { DefaultMessage = link };

            try
            {
                var target = new Uri(new Uri(url), link);
                if (target.Scheme != Uri.UriSchemeHttp && target.Scheme != Uri.UriSchemeHttps)
                {
                    // mailto:, tel:, javascript: and the like are not pages to fetch.
                    continue;
                }

                using var response = await httpClient.GetAsync(target, HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    seoItem.Status = AlertType.Error;
                    seoItem.DefaultMessage = text["seo_brokenLink", link, response.StatusCode];
                    seoItems.Add(seoItem);
                }
                // Add 500ms delay between requests
                await Task.Delay(500, cancellationToken);

            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // The run was stopped: not a broken link
                throw;
            }
            catch
            {
                seoItem.Status = AlertType.Error;
                seoItem.DefaultMessage = text["seo_unreachableLink", link];
                seoItems.Add(seoItem);
            }
        }

        if (links.Count > 50)
        {
            seoItems.Add(new SeoCheckResultItem
            {
                Status = AlertType.Warning,
                DefaultMessage = text["seo_tooManyLinks"].Value
            });
        }

        if (seoItems.Count == 0)
        {
            result.Items.Add(new SeoCheckResultItem
            {
                Status = AlertType.Success,
                DefaultMessage = text["seo_noBrokenLinks"].Value
            });
        }

        result.Items.AddRange(seoItems);
        return result;
    }

    public int SortOrder => 6;
}