using HtmlAgilityPack;
using ZauberCMS.Core.Seo.Models;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Seo.Checks;

public class ResponseStatusCheck(IHttpClientFactory httpClientFactory) : ISeoCheck
{
    public string Name => "Response Status Code Checker";

    public async Task<SeoCheckResult> Check(string url, HtmlDocument document, Content.Models.Content content)
    {
        var result = new SeoCheckResult(Name);
        var seoItem = new SeoCheckResultItem();

        try
        {
            // Use IHttpClientFactory to create a new instance of HttpClient
            var httpClient = httpClientFactory.CreateClient();
            // We grab a random user agent so we are not using the same one each time
            httpClient.DefaultRequestHeaders.Add("User-Agent", Constants.UserAgents.OrderBy(x => Guid.NewGuid()).FirstOrDefault());
            // Perform the HTTP GET request
            var response = await httpClient.GetAsync(url);
            var statusCode = (int)response.StatusCode;

            // Handle status codes
            if (statusCode is 200)
            {
                seoItem.Status = AlertType.Success;
                seoItem.DefaultMessage = "The response status code is 200 (OK).";
            }
            else if (statusCode is 301 or 302)
            {
                seoItem.Status = AlertType.Warning;
                seoItem.DefaultMessage = $"The response status code is {statusCode} (Redirect). Consider checking redirection setup.";
            }
            else
            {
                seoItem.Status = AlertType.Error;
                seoItem.DefaultMessage = $"The response status code is {statusCode}. This might indicate an issue with the URL.";
            }
        }
        catch (HttpRequestException ex)
        {
            // Handle network or HTTP request-specific errors
            seoItem.Status = AlertType.Error;
            seoItem.DefaultMessage = $"A network error occurred while checking the response: {ex.Message}";
        }
        catch (Exception ex)
        {
            // Handle general errors
            seoItem.Status = AlertType.Error;
            seoItem.DefaultMessage = $"An unexpected error occurred: {ex.Message}";
        }

        result.Items.Add(seoItem);

        return result;
    }

    public int SortOrder => 5;
}