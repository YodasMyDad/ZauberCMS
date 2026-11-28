using Blazored.Modal;
using Blazored.Modal.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using ZauberCMS.Components.Editors.Models;
using ZauberCMS.Components.Trees;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Localization;
using ZauberCMS.Core.Media.Models;
using ZauberCMS.RTE.Models;

namespace ZauberCMS.Components.Editors.ToolbarItems;

/// <summary>
/// Toolbar item for inserting media (images, documents, etc.) from the media library
/// </summary>
[method: ActivatorUtilitiesConstructor]
public class InsertMediaItem(IModalService modalService, IStringLocalizer<ZauberText> text) : ToolbarItemBase
{
    /// <summary>Made by hand rather than by the editor: its text is ZauberCMS's English.</summary>
    public InsertMediaItem(IModalService modalService) : this(modalService, EditorText.English)
    {
    }

    private Media? SelectedMedia { get; set; }
    private Guid? SelectedMediaId { get; set; }
    private IModalReference? Modal { get; set; }

    public override string Id => "insertMedia";
    public override string Label => text["editors_insertMedia"];
    public override string IconSvg =>
        "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\"><rect x=\"3\" y=\"3\" width=\"14\" height=\"14\" rx=\"2\"/><circle cx=\"8\" cy=\"8\" r=\"1.5\"/><path d=\"m17 13-3.5-3.5L5 17\"/><path d=\"M21 8v11a2 2 0 0 1-2 2H8\"/></svg>";
    public override bool IsToggle => false;

    public override async Task ExecuteAsync(IEditorApi api, CancellationToken cancellationToken)
    {
        await api.SaveSelectionAsync(cancellationToken);

        var parameters = new Dictionary<string, object>
        {
            { nameof(MediaTree.ValueChanged), EventCallback.Factory.Create<object>(this, OnMediaSelected) },
            { nameof(MediaTree.DisableContextMenu), true },
            { nameof(MediaTree.MediaTypes), new List<MediaType> { MediaType.Image } }
        };
        
        
        Modal = modalService.OpenSidePanel<MediaTree>(text["editors_insertMedia"], parameters);
        var result = await Modal.Result;
        
        if (result.Confirmed && SelectedMedia != null)
        {
            
            await api.RestoreSelectionAsync(cancellationToken);
            await InsertMedia(api, SelectedMedia);
            await api.ClearSavedSelectionAsync(cancellationToken);
        }
        else
        {
            // RTE 3.x: closing a panel no longer clears the saved selection for us
            await api.ClearSavedSelectionAsync(cancellationToken);
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

    private static async Task InsertMedia(IEditorApi api, Media media)
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
}