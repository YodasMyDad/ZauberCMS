using ZauberCMS.Core.Content.Models;

namespace ZauberCMS.Components.Editors.Models;

public class ContentPickerSettings
{
    public int? MaxAllowed { get; set; }
    public IEnumerable<Guid>? AllowedContentTypes { get; set; }
}