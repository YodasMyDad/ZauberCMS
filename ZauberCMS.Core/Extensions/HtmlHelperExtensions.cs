using System.Text;
using System.Web;
using MediatR;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;
using ZauberCMS.Core.Content.Interfaces;
using ZauberCMS.Core.Rendering;
using ZauberCMS.Core.Seo.Models;

namespace ZauberCMS.Core.Extensions;

public static class HtmlHelperExtensions
{
    public static async Task<IHtmlContent> GenerateMetaTags(this IHtmlHelper<dynamic> htmlHelper, string seoAlias,
        IMediator mediator, string? titleAlias = null, string? descriptionAlias = null)
    {
        if (htmlHelper.ViewData.Model is ZauberPageViewModel or Content.Models.Content)
        {
            var sb = new StringBuilder();

            if (htmlHelper.ViewData.Model is ZauberPageViewModel viewModel)
            {
                await RenderTags(htmlHelper, sb, mediator, seoAlias, viewModel.Name ?? string.Empty,
                    viewModel.Url, viewModel, titleAlias, descriptionAlias);
            }
            else if (htmlHelper.ViewData.Model is Content.Models.Content contentModel)
            {
                await RenderTags(htmlHelper, sb, mediator, seoAlias, contentModel.Name ?? string.Empty,
                    contentModel.Url, contentModel, titleAlias, descriptionAlias);
            }
            
            return new HtmlString(sb.ToString());
        }

        return new HtmlString(string.Empty);
    }

    private static async Task RenderTags(IHtmlHelper<dynamic> htmlHelper, StringBuilder sb, IMediator mediator,
        string seoAlias, string name, string? url, IHasPropertyValues model, string? titleAlias = null,
        string? descriptionAlias = null)
    {
        var metaData = model.GetValue<Meta>(seoAlias);
        if (metaData != null)
        {
            
            var httpContext = htmlHelper.ViewContext.HttpContext;
            var domain = httpContext.Request.Scheme + "://" + httpContext.Request.Host;

            // Determine if this is the home page using HttpContext
            var isHomePage = httpContext.Request.Path == "/";
            
            // Basic meta tags
            sb.AppendLine($"<title>{HttpUtility.HtmlEncode(metaData.PageTitle ?? name)}</title>");
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
            AddOpenGraphTag(sb, "og:title", metaData.PageTitle ?? name);
            AddOpenGraphTag(sb, "og:description", metaData.MetaDescription);
            AddOpenGraphTag(sb, "og:url", isHomePage ? domain : $"{domain}/{url}");

            AddMetaTag(sb, "robots",
                metaData.HideFromSearchEngines ? "noindex" : "index, follow, max-image-preview:large");
            
            AddLinkTag(sb, "canonical", isHomePage ? domain : $"{domain}/{url}");

        }
        else
        {
            // Basic meta tags
            sb.AppendLine($"<title>{HttpUtility.HtmlEncode(model.GetValue<string>(titleAlias ?? "") ?? name)}</title>");
            AddMetaTag(sb, "description", model.GetValue<string>(descriptionAlias ?? ""));
        }
    }

    private static void AddMetaTag(StringBuilder sb, string name, string? content)
    {
        if (!string.IsNullOrWhiteSpace(content))
        {
            sb.AppendLine(
                $"<meta name=\"{HttpUtility.HtmlEncode(name)}\" content=\"{HttpUtility.HtmlEncode(content)}\">");
        }
    }

    private static void AddOpenGraphTag(StringBuilder sb, string property, string? content)
    {
        if (!string.IsNullOrWhiteSpace(content))
        {
            sb.AppendLine(
                $"<meta property=\"{HttpUtility.HtmlEncode(property)}\" content=\"{HttpUtility.HtmlEncode(content)}\">");
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