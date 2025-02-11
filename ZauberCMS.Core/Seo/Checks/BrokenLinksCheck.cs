using HtmlAgilityPack;
using ZauberCMS.Core.Seo.Models;

namespace ZauberCMS.Core.Seo.Checks;

public class BrokenLinksCheck(IHttpClientFactory httpClientFactory) : ISeoCheck
{
    public string Name => "Broken Links Checker";

    public async Task<SeoCheckResult> Check(string url, HtmlDocument document, Content.Models.Content content)
    {
        var result = new SeoCheckResult(Name);
        var seoItems = new List<SeoCheckResultItem>();

        // Exclude nav, header, footer, and other non-content sections
        var contentNode = document.DocumentNode.SelectSingleNode("//body");
        if (contentNode == null)
        {
            result.Items.Add(new SeoCheckResultItem
            {
                Status = SeoCheckStatus.Warning,
                Message = "No valid <body> content found on the page."
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
                Status = SeoCheckStatus.Warning,
                Message = "No valid content links found on the page. Internal links within body content improve SEO."
            });
            return result;
        }

        var httpClient = httpClientFactory.CreateClient();
        
        // We grab a random user agent so we are not using the same one each time
        httpClient.DefaultRequestHeaders.Add("User-Agent", Constants.UserAgents.OrderBy(x => Guid.NewGuid()).FirstOrDefault());
        
        foreach (var link in links.Take(50))
        {
            var seoItem = new SeoCheckResultItem { Message = link };

            try
            {
                var absoluteUrl = link.StartsWith("http") ? link : new Uri(new Uri(url), link).ToString();
                var response = await httpClient.GetAsync(absoluteUrl);

                if (!response.IsSuccessStatusCode)
                {
                    seoItem.Status = SeoCheckStatus.Error;
                    seoItem.Message = $"Broken link: {link} (Status: {response.StatusCode})";
                    seoItems.Add(seoItem);
                }
                // Add 500ms delay between requests
                await Task.Delay(500);

            }
            catch
            {
                seoItem.Status = SeoCheckStatus.Error;
                seoItem.Message = $"Invalid or unreachable link: {link}";
                seoItems.Add(seoItem);
            }
        }

        if (links.Count > 50)
        {
            seoItems.Add(new SeoCheckResultItem
            {
                Status = SeoCheckStatus.Warning,
                Message = "More than 50 links. Only the first 50 have been checked."
            });
        }

        result.Items.AddRange(seoItems);
        return result;
    }

    public int SortOrder => 6;
}