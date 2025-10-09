using Blazored.Modal;
using Blazored.Modal.Services;
using Microsoft.AspNetCore.Components;
using ZauberCMS.Components.Trees;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Media.Models;
using ZauberCMS.RTE.Models;

namespace ZauberCMS.Components.Editors.ToolbarItems;

/// <summary>
/// Toolbar item for inserting media (images, documents, etc.) from the media library
/// </summary>
public class InsertMediaItem(IModalService modalService) : ToolbarItemBase
{
    private Media? SelectedMedia { get; set; }
    private Guid? SelectedMediaId { get; set; }
    private IModalReference? Modal { get; set; }

    public override string Id => "insertMedia";
    public override string Label => "Insert Media";
    public override string IconCss => "fa-photo-film";
    public override string Shortcut => "";
    public override ToolbarPlacement Placement => ToolbarPlacement.Media;
    public override ToolbarItemType ItemType => ToolbarItemType.Button;
    public override bool IsToggle => false;

    public override async Task ExecuteAsync(IEditorApi api)
    {
        await api.SaveSelectionRangeAsync();

        var parameters = new Dictionary<string, object>
        {
            { nameof(MediaTree.ValueChanged), EventCallback.Factory.Create<object>(this, OnMediaSelected) },
            { nameof(MediaTree.DisableContextMenu), true },
            { nameof(MediaTree.MediaTypes), new List<MediaType> { MediaType.Image } }
        };
        
        
        Modal = modalService.OpenSidePanel<MediaTree>("Insert Media", parameters);
        var result = await Modal.Result;
        
        if (result.Confirmed && SelectedMedia != null)
        {
            
            await api.RestoreSelectionRangeAsync();
            await InsertMedia(api, SelectedMedia);
            await api.ClearSavedSelectionRangeAsync();
        }
    }

    private void OnMediaSelected(object value)
    {
        if (value is Media { MediaType: MediaType.Image } media)
        {
            SelectedMediaId = media.Id;
            SelectedMedia = media;
            Modal?.Close(ModalResult.Ok(media));
        }
    }

    private async Task InsertMedia(IEditorApi api, Media media)
    {
        var html = media.MediaType == MediaType.Image
            ? BuildImageHtml(media)
            : BuildLinkHtml(media);

        await api.InsertHtmlAsync(html);
    }

    private static string BuildImageHtml(Media media)
    {
        var attributes = new List<string>
        {
            $"src=\"{media.Url ?? ""}\"",
            $"data-mediaid=\"{media.Id}\""
        };

        /*if (media.Width > 0)
        {
            attributes.Add($"width=\"{media.Width}\"");
        }

        if (media.Height > 0)
        {
            attributes.Add($"height=\"{media.Height}\"");
        }*/

        var altText = media.AltTag ?? media.Name ?? "";
        attributes.Add($"alt=\"{System.Web.HttpUtility.HtmlEncode(altText)}\"");

        return $"<img {string.Join(" ", attributes)} />";
    }

    private static string BuildLinkHtml(Media media)
    {
        var linkText = System.Web.HttpUtility.HtmlEncode(media.Name ?? "Download");
        return $"<a href=\"{media.Url ?? ""}\" data-mediaid=\"{media.Id}\">{linkText}</a>";
    }

    public override bool IsActive(EditorState state) => false;
}