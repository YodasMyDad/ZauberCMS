﻿using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZauberCMS.Components.Seo.Models;
using ZauberCMS.Core.Content.Commands;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Seo.Models;

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
                
                var rootPage = await mediator.GetContent(seoSitemap.RootContentId);
                if (rootPage == null) continue;
                
                if (rootPage.InternalRedirectId != null)
                {
                    rootPage = await mediator.GetContent(rootPage.InternalRedirectId);
                }

                if (rootPage != null) AddPageToSitemap(rootPage, sitemapEntries, seoSitemap, ns, true);

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
                        AddPageToSitemap(content, sitemapEntries, seoSitemap, ns);
                    }
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
                await using var writer = XmlWriter.Create(sitemapPath, settings);
                sitemap.Save(writer);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating sitemap.");
        }
    }
    
    private void AddPageToSitemap(Content content, List<XElement> sitemapEntries, SeoSitemap seoSitemap, XNamespace ns, bool isRootItem = false)
    {
        // Finally, need to see if this is using the SEO property and whether they
        // have ticked noindex or remove from sitemap
        var allowInSitemap = true;
        var seoProperty =
            content.ContentType?.ContentProperties.FirstOrDefault(x => x.Component == "ZauberCMS.Components.Editors.SeoProperty");
        
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
            var fullUrl = isRootItem ? seoSitemap.Domain : $"{seoSitemap.Domain}/{content.Url}";
            // Add content data to the sitemap list
            sitemapEntries.Add(new XElement(ns + "url",
                new XElement(ns + "loc", fullUrl),
                new XElement(ns + "lastmod", $"{content.DateUpdated:yyyy-MM-ddTHH:mm:sszzz}"),
                new XElement(ns + "changefreq", metaData != null ? metaData.ChangeFrequency.ToString().ToLower(CultureInfo.CurrentCulture) : "weekly"),
                new XElement(ns + "priority", metaData != null ? metaData.Priority.ToString(CultureInfo.CurrentCulture) : "0.5")
            ));   
        }
    }
}