namespace ZauberCMS.Core.Seo.Models;

public class SeoCheckResult
{
    public SeoCheckResult()
    {
        
    }

    public SeoCheckResult(string name)
    {
        Name = name;
        Message = name;
    }
    
    public string? Name { get; set; }
    public SeoCheckStatus Status { get; set; } = SeoCheckStatus.Success;
    public string? Message { get; set; }
}