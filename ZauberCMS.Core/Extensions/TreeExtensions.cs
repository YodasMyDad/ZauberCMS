using Microsoft.AspNetCore.Components;
using Radzen.Blazor;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Media.Models;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Extensions;

public static class TreeExtensions
{
    /// <summary>
    /// A tree node's text: its <c>Name</c>. For trees whose levels mix item types (a "Show more" row after the
    /// pages): Radzen's <c>TextProperty</c> reads the property as the first item's type and fails on the others.
    /// </summary>
    public static string TreeNodeText(object item) => item switch
    {
        TreeLoadMore more => more.Name,
        Content.Models.Content content => content.Name ?? string.Empty,
        Media.Models.Media media => media.Name ?? string.Empty,
        _ => item.GetType().GetProperty("Name")?.GetValue(item)?.ToString() ?? string.Empty
    };

    public static RenderFragment<T> CreateStandardTreeTemplate<T>() where T : class
    {
        return context => builder =>
        {
            var treeItem = context as RadzenTreeItem;
            switch (treeItem?.Value)
            {
                case TreeStub treeStub:
                    builder.OpenComponent<RadzenIcon>(0);
                    builder.AddAttribute(1, "Icon", treeStub.Icon);
                    builder.AddAttribute(2, "style", "font-weight: 300; color: dimgray;");
                    builder.CloseComponent();
                    builder.AddContent(3, treeStub.Name);
                    break;
                case TreeBranch branch:
                    builder.OpenComponent<RadzenIcon>(0);
                    builder.AddAttribute(1, "Icon", branch.Icon);
                    builder.AddAttribute(2, "style", "font-weight: 300; color: dimgray;");
                    builder.CloseComponent();
                    builder.AddContent(3, branch.Name);
                    break;
            }
        };
    }

    public static RenderFragment<T> CreateContentTreeTemplate<T>() where T : class
    {
        return context => builder =>
        {
            var treeItem = context as RadzenTreeItem;
            if (treeItem?.Value is TreeLoadMore more)
            {
                AddLoadMore(builder, more);
            }
            else if (treeItem?.Value is Content.Models.Content content)
            {
                // The draft's id is enough: trees don't load the draft itself
                var hasDraft = content.UnpublishedContent != null || content.UnpublishedContentId != null;
                var opacity = content.Deleted
                    ? "0.6"
                    : (hasDraft ? "0.6" : (content.Published ? "1" : "0.6"));
                var iconColor = content.ContentType?.Icon != null ? "dimgray" : "inherit";

                // Check if content has roles; if true, set icon color to red
                if (content.ContentRoles.Any())
                {
                    iconColor = "#AA0000";
                }

                if (!string.IsNullOrWhiteSpace(content.ContentType?.Icon))
                {
                    builder.OpenComponent<RadzenIcon>(0);
                    builder.AddAttribute(1, "Icon", content.ContentType.Icon);
                    builder.AddAttribute(2, "style", $"font-weight: 300; color: {iconColor}; opacity: {opacity};");
                    builder.CloseComponent();
                }

                builder.OpenElement(3, "span"); // using span as the container for the text
                builder.AddAttribute(4, "style",
                    $"opacity: {opacity}; display: inline-block; white-space: nowrap; overflow: hidden; text-overflow: ellipsis;");
                builder.AddContent(5, content.Name); // the text
                builder.CloseElement();
            }
        };
    }

    /// <summary>The "Show more" row of a node shown a page at a time (Roadmap D26).</summary>
    private static void AddLoadMore(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder, TreeLoadMore more)
    {
        builder.OpenComponent<RadzenIcon>(0);
        builder.AddAttribute(1, "Icon", "expand_more");
        builder.AddAttribute(2, "style", "font-weight: 300; color: var(--rz-primary);");
        builder.CloseComponent();
        builder.OpenElement(3, "span");
        builder.AddAttribute(4, "class", "zauber-tree-load-more");
        builder.AddAttribute(5, "style", "color: var(--rz-primary); white-space: nowrap;");
        builder.AddContent(6, more.Name);
        builder.CloseElement();
    }

    public static RenderFragment<T> CreateContentTypeTreeTemplate<T>() where T : class
    {
        return context => builder =>
        {
            var treeItem = context as RadzenTreeItem;
            if (treeItem?.Value is ContentType content)
            {
                var opacity = "1";
                var iconColor = content.Icon != null ? "dimgray" : "inherit";

                if (content.Icon.IsNullOrWhiteSpace())
                {
                    content.Icon = "description";
                }

                builder.OpenComponent<RadzenIcon>(0);
                builder.AddAttribute(1, "Icon", content.Icon);
                builder.AddAttribute(2, "style", $"font-weight: 300; color: {iconColor}; opacity: {opacity};");
                builder.CloseComponent();


                builder.OpenElement(3, "span");
                builder.AddAttribute(4, "style",
                    $"opacity: {opacity}; display: inline-block; white-space: nowrap; overflow: hidden; text-overflow: ellipsis;");

                builder.AddContent(5, content.Name); // the text
                builder.CloseElement();
            }
        };
    }
    
    public static RenderFragment<T> CreateMediaTreeTemplate<T>() where T : class
    {
        return context => builder =>
        {
            var treeItem = context as RadzenTreeItem;
            if (treeItem?.Value is TreeLoadMore more)
            {
                AddLoadMore(builder, more);
            }
            else if (treeItem?.Value is Media.Models.Media media)
            {
                if (media.MediaType == MediaType.Image)
                {
                    builder.OpenComponent<RadzenImage>(0);
                    builder.AddAttribute(1, "Path", $"{media.Url}??width=25&height=25&rmode=max");
                    builder.AddAttribute(2, "style", "width: 25px; height: 25px; margin-right:4px;");
                    builder.AddAttribute(2, "class", "rounded");
                    builder.AddAttribute(2, "AlternateText", media.Name);
                    builder.CloseComponent();
                }
                else
                {
                    builder.OpenComponent<RadzenIcon>(0);
                    builder.AddAttribute(1, "Icon", media.MediaType.GetIcon());
                    builder.AddAttribute(2, "style", "font-weight: 300; color: dimgray;");
                    builder.CloseComponent();
                }

                builder.AddContent(3, media.Name);
            }
        };
    }
}