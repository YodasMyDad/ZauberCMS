using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Seo.Models;

public class SeoCheckResultItem
{
    public AlertType Status { get; set; } = AlertType.Success;
    public string? Message { get; set; }
}