namespace ZauberCMS.Core.Seo.Models;

public class SeoCheckResult
{
    public SeoCheckResult()
    {
        
    }

    public SeoCheckResult(string name)
    {
        Name = name;
    }
    
    public string? Name { get; set; }
    public List<SeoCheckResultItem> Items { get; set; } = [];

}