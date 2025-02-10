namespace ZauberCMS.Core.Seo.Models;

public class Meta
{
    public string? PageTitle { get; set; }
    public string? MetaDescription { get; set; }
    public Guid? OpenGraphImage { get; set; }
    public bool HideFromSearchEngines { get; set; }
    public bool ExcludeFromSitemap { get; set; }
}