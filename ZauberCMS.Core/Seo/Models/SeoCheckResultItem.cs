namespace ZauberCMS.Core.Seo.Models;

public class SeoCheckResultItem
{
    public SeoCheckStatus Status { get; set; } = SeoCheckStatus.Success;
    public string? Message { get; set; }
}