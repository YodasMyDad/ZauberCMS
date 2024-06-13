using ZauberCMS.Core.Media.Models;

namespace ZauberCMS.Components.Editors.Models;

public class MediaPickerSettings
{
    public int? MaxAllowed { get; set; }
    public IEnumerable<MediaType>? AllowedMediaTypes { get; set; }
}