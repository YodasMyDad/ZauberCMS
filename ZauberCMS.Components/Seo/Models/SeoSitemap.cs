using ZauberCMS.Core.Content.Models;

namespace ZauberCMS.Components.Seo.Models;

public class SeoSitemap
{
    public string? Name { get; set; }
    public string? Domain { get; set; }
    public string? FileName { get; set; }
    public Guid RootContentId { get; set; }
    public Content? RootContent { get; set; }
    public List<Guid> ContentTypeIds { get; set; } = [];
}