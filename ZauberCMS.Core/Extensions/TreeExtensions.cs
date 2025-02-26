using Microsoft.AspNetCore.Components;
using Radzen.Blazor;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Extensions;

public static class TreeExtensions
{
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
            if (treeItem?.Value is Content.Models.Content content)
            {
                var opacity = content.UnpublishedContent != null ? "0.6" : (content.Published ? "1" : "0.6");
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
                builder.AddAttribute(4, "style", $"opacity: {opacity};"); // applying the opacity to the span
                builder.AddContent(5, content.Name); // the text
                builder.CloseElement();
            }
        };
    }
}