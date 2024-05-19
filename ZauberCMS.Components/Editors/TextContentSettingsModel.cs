using ZauberCMS.Core.Content.Interfaces;

namespace ZauberCMS.Components.Editors;

public class TextContentSettingsModel : IContentPropertySettings
{
    public int? MaxLength { get; set; }
}