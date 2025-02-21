using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Seo.Models;

public class SeoCheckResultItem
{
    public AlertType Status { get; set; } = AlertType.Success;
    public string? DefaultMessage { get; set; }
    public List<string> AdditionalMessages { get; set; } = [];
}