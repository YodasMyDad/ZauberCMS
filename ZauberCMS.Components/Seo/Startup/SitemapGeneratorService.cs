using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZauberCMS.Components.Seo.Models;
using ZauberCMS.Core.Content.Interfaces;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Content.Parameters;
using ZauberCMS.Core.Data.Interfaces;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Installer;
using ZauberCMS.Core.Seo.Models;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Components.Seo.Startup;

public class SitemapGeneratorService(ILogger<SitemapGeneratorService> logger, IWebHostEnvironment env, IServiceProvider serviceProvider)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Sitemap Generator Hosted Service is starting.");

        using PeriodicTimer timer = new(TimeSpan.FromHours(6));

        try
        {
            do
            {
                await GenerateSitemapAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("Sitemap Generator Hosted Service is stopping.");
        }
    }

    // A shutdown stops a run that is under way (ZA-111): the file being written is left as it was
    private async Task GenerateSitemapAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!serviceProvider.GetRequiredService<InstallState>().IsDatabaseConfigured)
            {
                // The installer has not been given a database yet; the next run is in six hours
                return;
            }

            using var scope = serviceProvider.CreateScope();
            var contentService = scope.ServiceProvider.GetRequiredService<IContentService>();
            var dataService = scope.ServiceProvider.GetRequiredService<IDataService>();

            // Firstly get the site map data
            var sitemapGlobalData = await dataService.GetGlobalData<List<SeoSitemap>>(SeoConstants.SeoSitemapName) ?? [];
            foreach (var seoSitemap in sitemapGlobalData)
            {
                cancellationToken.ThrowIfCancellationRequested();
                // Define your namespace for sitemap
                XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";

                // Path to the sitemap file
                var sitemapPath = Path.Combine(env.WebRootPath, $"{seoSitemap.FileName}.xml");

                // Prepare a list to hold sitemap entries
                var sitemapEntries = new List<XElement>();
                
                var rootPage = await contentService.GetContent(seoSitemap.RootContentId);
                if (rootPage == null) continue;
                
                // A root that sends visitors to another page is listed as that page. Guid.Empty means no redirect:
                // looking it up found nothing and the home page was left out (ZA-114).
                if (rootPage.InternalRedirectId is { } redirectId && redirectId != Guid.Empty)
                {
                    rootPage = await contentService.GetContent(redirectId) ?? rootPage;
                }

                var seoProperties = new Dictionary<Guid, PropertyType?>();
                if (rootPage != null) await AddPageToSitemap(rootPage, sitemapEntries, seoSitemap, ns, contentService, seoProperties, true);

                // Every page of the chosen types, a page of results at a time: one query stopped at 2000 (ZA-073).
                // A sitemap file holds at most 50,000 addresses.
                for (var pageIndex = 1; sitemapEntries.Count < MaxSitemapEntries; pageIndex++)
                {
                    var contentItems = await contentService.QueryContentAsync(new QueryContentParameters
                    {
                        PageIndex = pageIndex,
                        AmountPerPage = QueryPageSize,
                        OrderBy = GetContentsOrderBy.DateCreated,
                        WhereClause = content => seoSitemap.ContentTypeIds.Contains(content.ContentTypeId)
                    }, cancellationToken);

                    foreach (var content in contentItems.Items)
                    {
                        // Only allow if this item is under the root id
                        if (content.Path.Contains(seoSitemap.RootContentId) && sitemapEntries.Count < MaxSitemapEntries)
                        {
                            await AddPageToSitemap(content, sitemapEntries, seoSitemap, ns, contentService, seoProperties);
                        }
                    }

                    if (contentItems.IsLastPage)
                    {
                        break;
                    }
                }

                if (sitemapEntries.Count >= MaxSitemapEntries)
                {
                    logger.LogWarning("Sitemap {FileName} has reached {Max} addresses; the rest are left out",
                        seoSitemap.FileName, MaxSitemapEntries);
                }
    
                // Create the sitemap XML document
                var sitemap = new XDocument(
                    new XElement(ns + "urlset",
                        sitemapEntries
                    )
                );

                // Write the sitemap to the file
                // Create settings to include XML declaration
                var settings = new XmlWriterSettings
                {
                    Indent = true,
                    Async = true,
                    Encoding = Encoding.UTF8,
                    OmitXmlDeclaration = false // Ensure XML declaration is included
                };

                // Write the sitemap to the file with XML declaration
                // Written to a file of its own and then moved over the sitemap: a run that is stopped, or fails,
                // half-way leaves the previous sitemap as it was
                var temporaryPath = sitemapPath + ".tmp";
                try
                {
                    await using (var writer = XmlWriter.Create(temporaryPath, settings))
                    {
                        await sitemap.SaveAsync(writer, cancellationToken);
                    }

                    File.Move(temporaryPath, sitemapPath, overwrite: true);
                }
                finally
                {
                    File.Delete(temporaryPath);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating sitemap.");
        }
    }
    
    /// <summary>The most addresses one sitemap file may hold (sitemaps.org).</summary>
    private const int MaxSitemapEntries = 50_000;

    private const int QueryPageSize = 500;

    private static async Task<PropertyType?> FindSeoPropertyAsync(Content content, IContentService contentService,
        Dictionary<Guid, PropertyType?> seoProperties)
    {
        if (content.ContentType == null) return null;
        // Once per content type and run: the compositions were looked up again for every page
        if (seoProperties.TryGetValue(content.ContentType.Id, out var known)) return known;
        var found = await FindSeoPropertyOfTypeAsync(content.ContentType, contentService);
        seoProperties[content.ContentType.Id] = found;
        return found;
    }

    private static async Task<PropertyType?> FindSeoPropertyOfTypeAsync(ContentType contentType,
        IContentService contentService)
    {
        
        // Check main ContentType first
        var seoProperty = contentType.ContentProperties
            .FirstOrDefault(x => x.Component == "ZauberCMS.Components.Editors.SeoProperty");
        
        if (seoProperty != null) return seoProperty;
        
        // Check compositions if not found
        if (contentType.CompositionIds.Count > 0)
        {
            foreach (var compositionId in contentType.CompositionIds)
            {
                var compositionType = await contentService.GetContentTypeAsync(
                    new GetContentTypeParameters { Id = compositionId });
                
                if (compositionType != null)
                {
                    seoProperty = compositionType.ContentProperties
                        .FirstOrDefault(x => x.Component == "ZauberCMS.Components.Editors.SeoProperty");
                    
                    if (seoProperty != null) return seoProperty;
                }
            }
        }
        
        return null;
    }
    
    private static async Task AddPageToSitemap(Content content, List<XElement> sitemapEntries, SeoSitemap seoSitemap,
        XNamespace ns, IContentService contentService, Dictionary<Guid, PropertyType?> seoProperties, bool isRootItem = false)
    {
        // Finally, need to see if this is using the SEO property and whether they
        // have ticked noindex or remove from sitemap
        var allowInSitemap = true;
        var seoProperty = await FindSeoPropertyAsync(content, contentService, seoProperties);
        
        Meta? metaData = null;
        if (seoProperty != null)
        {
            // We have an SEO property
            metaData = content.GetValue<Meta>(seoProperty.Alias ?? "meh");
            if (metaData is { ExcludeFromSitemap: true } or { HideFromSearchEngines: true })
            {
                allowInSitemap = false;
            }
        }

        if (allowInSitemap)
        {
            var fullUrl = isRootItem ? seoSitemap.Domain : $"{seoSitemap.Domain}/{content.Url()}";
            // Add content data to the sitemap list
            sitemapEntries.Add(new XElement(ns + "url",
                new XElement(ns + "loc", fullUrl),
                // Stored in UTC: "zzz" printed the server's own offset after a UTC time
                new XElement(ns + "lastmod", content.DateUpdated.ToString("yyyy-MM-ddTHH:mm:ss'Z'", CultureInfo.InvariantCulture)),
                new XElement(ns + "changefreq", metaData != null ? metaData.ChangeFrequency.ToString().ToLowerInvariant() : "weekly"),
                // The sitemap format wants 0.5, never the server culture's 0,5
                new XElement(ns + "priority", metaData != null ? metaData.Priority.ToString(CultureInfo.InvariantCulture) : "0.5")
            ));   
        }
    }
}