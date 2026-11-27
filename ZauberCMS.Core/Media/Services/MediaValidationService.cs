using System.Globalization;
using HtmlAgilityPack;
using Radzen;
using ZauberCMS.Core.Media.Interfaces;
using ZauberCMS.Core.Media.Parameters;

namespace ZauberCMS.Core.Media.Services;

public class MediaValidationService(IMediaService mediaService, NotificationService notificationService)
{
    /// <summary>
    /// Validates and updates media references in HTML content.
    /// Updates URLs for moved media and removes deleted media items.
    /// </summary>
    /// <param name="html">The HTML content to validate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated HTML content</returns>
    public async Task<string> ValidateAndUpdateMediaReferencesAsync(string html, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return html;
        }

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var elementsWithMediaId = doc.DocumentNode.SelectNodes("//*[@data-mediaid]");
        
        if (elementsWithMediaId == null || elementsWithMediaId.Count == 0)
        {
            return html;
        }

        var updatedItems = new List<string>();
        var deletedItems = new List<string>();
        var hasChanges = false;

        foreach (var element in elementsWithMediaId)
        {
            var mediaIdStr = element.GetAttributeValue("data-mediaid", "");
            
            if (!Guid.TryParse(mediaIdStr, out var mediaId))
            {
                continue;
            }

            var media = await mediaService.GetMediaAsync(new GetMediaParameters
            {
                Id = mediaId,
                AsNoTracking = true
            }, cancellationToken);

            if (media == null)
            {
                // Media was deleted - remove the element
                deletedItems.Add(element.GetAttributeValue("alt", element.GetAttributeValue("data-mediaid", "Unknown")));
                element.Remove();
                hasChanges = true;
            }
            else
            {
                // Check if URL has changed
                var currentUrl = element.Name.Equals("img", StringComparison.OrdinalIgnoreCase)
                    ? element.GetAttributeValue("src", "")
                    : element.GetAttributeValue("href", "");

                if (!string.IsNullOrEmpty(media.Url) && !currentUrl.Equals(media.Url, StringComparison.OrdinalIgnoreCase))
                {
                    // Update the URL
                    if (element.Name.Equals("img", StringComparison.OrdinalIgnoreCase))
                    {
                        element.SetAttributeValue("src", media.Url);
                        
                        // Also update width/height if they've changed
                        if (media.Width > 0)
                        {
                            element.SetAttributeValue("width", media.Width.ToString(CultureInfo.InvariantCulture));
                        }
                        if (media.Height > 0)
                        {
                            element.SetAttributeValue("height", media.Height.ToString(CultureInfo.InvariantCulture));
                        }
                        
                        // Update alt tag if available
                        if (!string.IsNullOrWhiteSpace(media.AltTag))
                        {
                            element.SetAttributeValue("alt", media.AltTag);
                        }
                    }
                    else
                    {
                        element.SetAttributeValue("href", media.Url);
                    }
                    
                    updatedItems.Add(media.Name ?? "Unknown");
                    hasChanges = true;
                }
            }
        }

        // Show notifications if changes were made
        if (updatedItems.Count != 0)
        {
            notificationService.Notify(new NotificationMessage
            {
                Severity = NotificationSeverity.Info,
                Summary = "Media Updated",
                Detail = $"Media URLs have changed. {updatedItems.Count} item(s) updated. Save to apply changes.",
                Duration = 6000
            });
        }

        if (deletedItems.Count != 0)
        {
            notificationService.Notify(new NotificationMessage
            {
                Severity = NotificationSeverity.Warning,
                Summary = "Media Deleted",
                Detail = $"{deletedItems.Count} media item(s) have been deleted and removed from content.",
                Duration = 6000
            });
        }

        return hasChanges ? doc.DocumentNode.OuterHtml : html;
    }
}

