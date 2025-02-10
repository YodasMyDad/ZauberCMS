using System.Text;
using System.Web;
using MediatR;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;
using ZauberCMS.Core.Rendering;
using ZauberCMS.Core.Seo.Models;

namespace ZauberCMS.Core.Extensions;

public static class HtmlHelperExtensions
{
    public static IHtmlContent GenerateMetaTags(this IHtmlHelper<dynamic> htmlHelper, string seoAlias, IMediator mediator, string? titleAlias = null, string? descriptionAlias = null)
    {
        if (htmlHelper.ViewData.Model is ZauberPageViewModel model)
        {
            var sb = new StringBuilder();

            var metaData = model.GetValue<Meta>(seoAlias);
            if (metaData != null)
            {
                
                // Basic meta tags
                sb.AppendLine($"<title>{HttpUtility.HtmlEncode(metaData.PageTitle ?? model.Name)}</title>");
                AddMetaTag(sb, "description", metaData.MetaDescription);
                
                
                    /*<meta name="robots" content="index, follow, max-image-preview:large, max-snippet:-1, max-video-preview:-1"/>
                    <link rel="canonical" href="@url"/>
                    <meta property="og:locale" content="en_GB"/>
                    <meta property="og:type" content="article"/>
                    <meta property="og:title" content="@pageTitle"/>
                    <meta property="og:description" content="@metaDescription"/>
                    <meta property="og:url" content="@url"/>
                    <meta property="og:site_name" content="Lee"/>
                    <meta property="article:published_time"
                content="@Model.DateCreated.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssK")"/>
                    @if (Model.HeaderImage != null)
                {
                    <meta property="og:image"
                    content="@($"https://lee.uk/{Model.HeaderImage.Url}?width=1441&height=811&mode=stretch")"/>
                        <meta property="og:image:width" content="1441"/>
                        <meta property="og:image:height" content="811"/>
                        <meta property="og:image:type" content="image/jpeg"/>
                }
                <meta name="author" content="Lee M"/>
                    <meta name="twitter:card" content="summary_large_image"/>
                    <meta name="twitter:creator" content="https://twitter.com/yodasmydad"/>*/
                
            }
            else
            {
                // Basic meta tags
                sb.AppendLine($"<title>{HttpUtility.HtmlEncode(model.GetValue<string>(titleAlias ?? "") ?? model.Name)}</title>");
                AddMetaTag(sb, "description", model.GetValue<string>(descriptionAlias ?? ""));   
            }
        
            // Language meta
            /*if (model.Language != null)
            {
                AddMetaTag(sb, "language", model.Language.Code);
            }*/

            // OpenGraph tags
            /*AddOpenGraphTag(sb, "og:title", model.Name);
            AddOpenGraphTag(sb, "og:type", "website");
            AddOpenGraphTag(sb, "og:url", model.Url);
            AddOpenGraphTag(sb, "og:updated_time", model.DateUpdated.ToString("O"));*/

            return new HtmlString(sb.ToString());
        }
        
        return new HtmlString(string.Empty);
    }

    private static void AddMetaTag(StringBuilder sb, string name, string? content)
    {
        if (!string.IsNullOrWhiteSpace(content))
        {
            sb.AppendLine($"<meta name=\"{HttpUtility.HtmlEncode(name)}\" content=\"{HttpUtility.HtmlEncode(content)}\">");
        }
    }

    private static void AddOpenGraphTag(StringBuilder sb, string property, string? content)
    {
        if (!string.IsNullOrWhiteSpace(content))
        {
            sb.AppendLine($"<meta property=\"{HttpUtility.HtmlEncode(property)}\" content=\"{HttpUtility.HtmlEncode(content)}\">");
        }
    }
    
    private static void AddLinkTag(StringBuilder sb, string rel, string? href)
    {
        if (!string.IsNullOrWhiteSpace(href))
        {
            sb.AppendLine($"<link rel=\"{HttpUtility.HtmlEncode(rel)}\" href=\"{HttpUtility.HtmlEncode(href)}\">");
        }
    }

}