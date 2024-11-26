namespace ZauberCMS.Core.Content.Models;

public class EntryContentResult
{
    public Guid? ContentId { get; set; }
    public bool IncludeChildren { get; set; }
    public string? LanguageIsoCode { get; set; }
}