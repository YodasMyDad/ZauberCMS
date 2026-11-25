using Blazored.Modal.Services;
using ZauberCMS.Components.Editors.Dialogs;
using ZauberCMS.Components.Editors.Dialogs.Models;
using ZauberCMS.Core.Content.Interfaces;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Extensions;
using ZauberCMS.RTE.Models;

namespace ZauberCMS.Components.Editors.ToolbarItems;

/// <summary>
/// Toolbar item for inserting links to content or external URLs
/// </summary>
public class InsertContentLinkItem(IModalService modalService) : ToolbarItemBase
{
    public override string Id => "insertContentLink";
    public override string Label => "Insert Link";
    public override string Tooltip => "Insert Link";
    public override string IconSvg =>
        "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\"><path d=\"M10 13a5 5 0 0 0 7.07 0l3-3a5 5 0 0 0-7.07-7.07l-1.5 1.5\"/><path d=\"M14 11a5 5 0 0 0-7.07 0l-3 3a5 5 0 0 0 7.07 7.07l1.5-1.5\"/></svg>";
    public override string Shortcut => "Control+k";
    public override bool IsToggle => true;
    public override IReadOnlyList<string> TrackedTags => ["a"];
    public override string PrimaryTag => "a";

    public override async Task ExecuteAsync(IEditorApi api, CancellationToken cancellationToken)
    {
        var selection = await api.GetSelectionAsync(cancellationToken);
        var existingLink = await api.GetLinkAtCursorAsync(cancellationToken);

        // Only open if there's selected text or cursor is on a link
        if ((selection != null && !string.IsNullOrWhiteSpace(selection.Text)) || existingLink != null)
        {
            if (existingLink != null)
            {
                // Select the whole existing link while the editor still has focus, so the saved selection
                // covers it and the new <a> replaces the old one instead of nesting inside it.
                await api.SelectLinkAtCursorAsync(cancellationToken);
            }

            await api.SaveSelectionAsync(cancellationToken);

            var parameters = new Dictionary<string, object>();
            
            if (selection != null)
            {
                parameters.Add(nameof(ContentLinkDialog.CurrentSelection), selection);
            }
            
            if (existingLink != null)
            {
                parameters.Add(nameof(ContentLinkDialog.ExistingLink), existingLink);
            }

            var modal = modalService.OpenSidePanel<ContentLinkDialog>("Insert Link", parameters);
            var result = await modal.Result;

            if (result.Confirmed && result.Data != null)
            {
                await api.RestoreSelectionAsync(cancellationToken);

                // Handle the result based on type
                if (result.Data is ContentLinkResult contentLinkResult)
                {
                    await InsertContentLink(api, contentLinkResult.Content, contentLinkResult.LinkText, contentLinkResult.Url);
                }
                else if (result.Data is ManualLinkResult manualLinkResult)
                {
                    await InsertManualLink(api, manualLinkResult.Url, manualLinkResult.Text, 
                        manualLinkResult.Title, manualLinkResult.Target, manualLinkResult.Rel, manualLinkResult.ContentId);
                }

                await api.ClearSavedSelectionAsync(cancellationToken);
            }
            else
            {
                // RTE 3.x: closing a panel no longer clears the saved selection for us
                await api.ClearSavedSelectionAsync(cancellationToken);
            }
        }
    }

    private async Task InsertContentLink(IEditorApi api, Content content, string linkText, string url)
    {
        var attributes = new List<string>
        {
            $"href=\"{url}\"",
            $"data-contentid=\"{content.Id}\""
        };

        var html = $"<a {string.Join(" ", attributes)}>{System.Web.HttpUtility.HtmlEncode(linkText)}</a>";
        await api.InsertHtmlAsync(html);
    }

    private async Task InsertManualLink(IEditorApi api, string url, string text, string? title, string? target, string? rel, Guid? contentId = null)
    {
        var attrs = new Dictionary<string, string>
        {
            ["href"] = url
        };

        if (!string.IsNullOrWhiteSpace(target))
        {
            attrs["target"] = target;
        }

        if (!string.IsNullOrWhiteSpace(rel))
        {
            attrs["rel"] = rel;
        }

        if (!string.IsNullOrWhiteSpace(title))
        {
            attrs["title"] = title;
        }
        
        // Add data-contentid if link originated from content selection
        if (contentId.HasValue)
        {
            attrs["data-contentid"] = contentId.Value.ToString();
        }

        // Build the link HTML
        var attrString = string.Join(" ", attrs.Select(kvp => $"{kvp.Key}=\"{System.Web.HttpUtility.HtmlAttributeEncode(kvp.Value)}\""));
        var html = $"<a {attrString}>{System.Web.HttpUtility.HtmlEncode(text)}</a>";

        await api.InsertHtmlAsync(html);
    }
}

