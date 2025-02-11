using System.Text;
using System.Xml.Linq;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZauberCMS.Components.Admin.Seo.Models;
using ZauberCMS.Core.Content.Commands;
using ZauberCMS.Core.Extensions;

namespace ZauberCMS.Components.Admin.Seo.Startup;

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
                await GenerateSitemapAsync();
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Sitemap Generator Hosted Service is stopping.");
        }
    }

    private async Task GenerateSitemapAsync()
    {
        try
        {
            using var scope = serviceProvider.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

            // Firstly get the site map data
            var sitemapGlobalData = await mediator.GetGlobalData<List<SeoSitemap>>(SeoConstants.SeoSitemapName) ?? [];
            foreach (var seoSitemap in sitemapGlobalData)
            {
                // Define your namespace for sitemap
                XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";

                // Path to the sitemap file
                var sitemapPath = Path.Combine(env.WebRootPath, $"{seoSitemap.FileName}.xml");

                // Prepare a list to hold sitemap entries
                var sitemapEntries = new List<XElement>();
                
                var contentItems = await mediator.QueryContent(new QueryContentCommand
                {
                    AmountPerPage = 2000,
                    WhereClause = content => seoSitemap.ContentTypeIds.Contains(content.ContentTypeId)
                });

                foreach (var content in contentItems.Items)
                {
                    // Only allow if this item is under the root id
                    if (content.Path.Contains(seoSitemap.RootContentId))
                    {
                        // Add content data to the sitemap list
                        sitemapEntries.Add(new XElement(ns + "url",
                            new XElement(ns + "loc", $"{seoSitemap.Domain}/{content.Url}"),
                            new XElement(ns + "lastmod", $"{content.DateUpdated:yyyy-MM-ddTHH:mm:sszzz}"),
                            new XElement(ns + "changefreq", "weekly"), // Need to make configurable
                            new XElement(ns + "priority", "0.5") // Need to make configurable
                        ));

                    }
                }
    
                // Create the sitemap XML document
                var sitemap = new XDocument(
                    new XElement(ns + "urlset",
                        sitemapEntries
                    )
                );

                // Write the sitemap to the file
                await File.WriteAllTextAsync(sitemapPath, sitemap.ToString(), Encoding.UTF8);

            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating sitemap.");
        }
    }
}