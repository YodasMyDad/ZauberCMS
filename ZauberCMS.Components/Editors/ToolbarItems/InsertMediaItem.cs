using ZauberCMS.RTE.Models;

namespace ZauberCMS.Components.Editors.ToolbarItems;

/// <summary>
/// Toolbar item for inserting media (images, documents, etc.) from the media library
/// </summary>
public class InsertMediaItem : ToolbarItemBase
{
    public override string Id => "insertMedia";
    public override string Label => "Insert Media";
    public override string IconCss => "fa-photo-film";
    public override string Shortcut => "";
    public override ToolbarPlacement Placement => ToolbarPlacement.Media;
    public override ToolbarItemType ItemType => ToolbarItemType.Button;
    public override bool IsToggle => false;

    public override async Task ExecuteAsync(IEditorApi api)
    {
        await api.OpenPanelAsync(typeof(Panels.InsertMediaPanel));
    }

    public override bool IsActive(EditorState state) => false;
}

