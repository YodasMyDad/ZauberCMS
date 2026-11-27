using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
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

    public static RenderFragment<T> CreateStandardTreeTemplate<T>() where T : class => StandardTreeTemplate<T>(null);

    /// <summary>
    /// As <see cref="CreateStandardTreeTemplate{T}()"/>, with a <see cref="TreeLoadMore"/> row shown as a button that
    /// calls <paramref name="loadMore"/>: choosing it does not select the row, so the selected node keeps its
    /// highlight (ZA-161).
    /// </summary>
    /// <param name="receiver">The tree component (it re-renders after the click).</param>
    /// <param name="loadMore">Loads the next page in the row's place.</param>
    public static RenderFragment<T> CreateStandardTreeTemplate<T>(object receiver, Func<TreeLoadMore, Task> loadMore)
        where T : class => StandardTreeTemplate<T>(new LoadMoreHandler(receiver, loadMore));

    private static RenderFragment<T> StandardTreeTemplate<T>(LoadMoreHandler? loadMore) where T : class
    {
        return context => builder =>
        {
            var treeItem = context as RadzenTreeItem;
            switch (treeItem?.Value)
            {
                case TreeLoadMore more:
                    AddLoadMore(builder, more, loadMore);
                    break;
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

    public static RenderFragment<T> CreateContentTreeTemplate<T>() where T : class => ContentTreeTemplate<T>(null);

    /// <summary>
    /// As <see cref="CreateContentTreeTemplate{T}()"/>, with the "Show more" row as a button that calls
    /// <paramref name="loadMore"/> without selecting the row (ZA-161).
    /// </summary>
    /// <param name="receiver">The tree component (it re-renders after the click).</param>
    /// <param name="loadMore">Loads the next page in the row's place.</param>
    public static RenderFragment<T> CreateContentTreeTemplate<T>(object receiver, Func<TreeLoadMore, Task> loadMore)
        where T : class => ContentTreeTemplate<T>(new LoadMoreHandler(receiver, loadMore));

    private static RenderFragment<T> ContentTreeTemplate<T>(LoadMoreHandler? loadMore) where T : class
    {
        return context => builder =>
        {
            var treeItem = context as RadzenTreeItem;
            if (treeItem?.Value is TreeLoadMore more)
            {
                AddLoadMore(builder, more, loadMore);
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

    /// <summary>What a "Show more" button calls, and the component that re-renders after it.</summary>
    private sealed record LoadMoreHandler(object Receiver, Func<TreeLoadMore, Task> LoadMore);

    /// <summary>
    /// The "Show more" row of a node shown a page at a time (Roadmap D26). With a handler it is a button whose click
    /// stops at the button, so the tree never selects the row (ZA-161). Without one, the tree's change handler loads
    /// the page when the row is selected; the keyboard still selects it, so trees handle both.
    /// </summary>
    private static void AddLoadMore(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder,
        TreeLoadMore more, LoadMoreHandler? loadMore)
    {
        if (loadMore != null)
        {
            builder.OpenElement(0, "button");
            builder.AddAttribute(1, "type", "button");
            // Not a Tab stop inside the tree: the arrow keys reach the row and Enter selects it, which loads too
            builder.AddAttribute(2, "tabindex", "-1");
            builder.AddAttribute(3, "class", "zauber-tree-load-more");
            builder.AddAttribute(4, "style",
                "display: inline-flex; align-items: center; gap: 0.25rem; padding: 0; border: 0; background: none; " +
                "font: inherit; color: var(--rz-primary); white-space: nowrap; cursor: pointer;");
            builder.AddAttribute(5, "onclick",
                EventCallback.Factory.Create<MouseEventArgs>(loadMore.Receiver,
                    () => loadMore.LoadMore(more)));
            builder.AddEventStopPropagationAttribute(6, "onclick", true);
            builder.OpenComponent<RadzenIcon>(7);
            builder.AddAttribute(8, "Icon", "expand_more");
            builder.AddAttribute(9, "style", "font-weight: 300;");
            builder.CloseComponent();
            builder.AddContent(10, more.Name);
            builder.CloseElement();
            return;
        }

        builder.OpenComponent<RadzenIcon>(11);
        builder.AddAttribute(12, "Icon", "expand_more");
        builder.AddAttribute(13, "style", "font-weight: 300; color: var(--rz-primary);");
        builder.CloseComponent();
        builder.OpenElement(14, "span");
        builder.AddAttribute(15, "class", "zauber-tree-load-more");
        builder.AddAttribute(16, "style", "color: var(--rz-primary); white-space: nowrap;");
        builder.AddContent(17, more.Name);
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
    
    public static RenderFragment<T> CreateMediaTreeTemplate<T>() where T : class => MediaTreeTemplate<T>(null);

    /// <summary>
    /// As <see cref="CreateMediaTreeTemplate{T}()"/>, with the "Show more" row as a button that calls
    /// <paramref name="loadMore"/> without selecting the row (ZA-161).
    /// </summary>
    /// <param name="receiver">The tree component (it re-renders after the click).</param>
    /// <param name="loadMore">Loads the next page in the row's place.</param>
    public static RenderFragment<T> CreateMediaTreeTemplate<T>(object receiver, Func<TreeLoadMore, Task> loadMore)
        where T : class => MediaTreeTemplate<T>(new LoadMoreHandler(receiver, loadMore));

    private static RenderFragment<T> MediaTreeTemplate<T>(LoadMoreHandler? loadMore) where T : class
    {
        return context => builder =>
        {
            var treeItem = context as RadzenTreeItem;
            if (treeItem?.Value is TreeLoadMore more)
            {
                AddLoadMore(builder, more, loadMore);
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