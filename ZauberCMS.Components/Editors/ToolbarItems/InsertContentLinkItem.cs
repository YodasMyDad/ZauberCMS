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
    public override string Tooltip => "Insert Link (Ctrl+K)";
    public override string IconCss => "fa-link";
    public override string Shortcut => "Control+k";
    public override ToolbarPlacement Placement => ToolbarPlacement.Insert;
    public override ToolbarItemType ItemType => ToolbarItemType.Button;
    public override bool IsToggle => true;
    public override string[] TrackedTags => ["a"];
    public override string PrimaryTag => "a";

    public override async Task ExecuteAsync(IEditorApi api)
    {
        var selection = await api.GetSelectionAsync();
        var existingLink = await api.GetLinkAtCursorAsync();

        // Only open if there's selected text or cursor is on a link
        if ((selection != null && !string.IsNullOrWhiteSpace(selection.SelectedText)) || existingLink != null)
        {
            await api.SaveSelectionRangeAsync();

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
                await api.RestoreSelectionRangeAsync();

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

                await api.ClearSavedSelectionRangeAsync();
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

    public override bool IsActive(EditorState state) => state.ActiveMarks.Contains("a");
}

