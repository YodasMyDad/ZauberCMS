using System.Text;
using System.Web;
using MediatR;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;
using ZauberCMS.Core.Media.Commands;
using ZauberCMS.Core.Rendering;
using ZauberCMS.Core.Seo.Models;

namespace ZauberCMS.Core.Extensions;

public static class HtmlHelperExtensions
{
    public static async Task<IHtmlContent> GenerateMetaTags(this IHtmlHelper<dynamic> htmlHelper, string seoAlias, IMediator mediator, string? titleAlias = null, string? descriptionAlias = null)
    {
        if (htmlHelper.ViewData.Model is ZauberPageViewModel model)
        {
            var sb = new StringBuilder();

            var metaData = model.GetValue<Meta>(seoAlias);
            if (metaData != null)
            {
                var domain = htmlHelper.ViewContext.HttpContext.Request.Scheme + "://" + htmlHelper.ViewContext.HttpContext.Request.Host;
                
                // Basic meta tags
                sb.AppendLine($"<title>{HttpUtility.HtmlEncode(metaData.PageTitle ?? model.Name)}</title>");
                AddMetaTag(sb, "description", metaData.MetaDescription);

                if (metaData.OpenGraphImage != null)
                {
                    var result = await mediator.GetMedia(metaData.OpenGraphImage.Value);
                    if (result != null)
                    {
                        AddOpenGraphTag(sb, "og:image", $"{domain}/{result.Url}?width=1200&height=630&mode=stretch");
                        AddOpenGraphTag(sb, "og:image:width", "1200");
                        AddOpenGraphTag(sb, "og:image:height", "630");
                        AddOpenGraphTag(sb, "og:image:type", "image/jpeg");
                        AddMetaTag(sb, "twitter:card", "summary_large_image");
                    }
                }
                AddOpenGraphTag(sb, "og:locale", System.Globalization.CultureInfo.CurrentCulture.Name);
                AddOpenGraphTag(sb, "og:title", metaData.PageTitle ?? model.Name);
                AddOpenGraphTag(sb, "og:description", metaData.MetaDescription);
                AddOpenGraphTag(sb, "og:url", $"{domain}/{model.Url}");
                
                AddMetaTag(sb, "robots",
                    metaData.HideFromSearchEngines ? "noindex" : "index, follow, max-image-preview:large");

                AddLinkTag(sb, "canonical", $"{domain}/{model.Url}");
            }
            else
            {
                // Basic meta tags
                sb.AppendLine($"<title>{HttpUtility.HtmlEncode(model.GetValue<string>(titleAlias ?? "") ?? model.Name)}</title>");
                AddMetaTag(sb, "description", model.GetValue<string>(descriptionAlias ?? ""));   
            }
            
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