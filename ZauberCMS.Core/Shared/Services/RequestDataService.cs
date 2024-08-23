namespace ZauberCMS.Core.Shared.Services;

public class RequestDataService
{
    public Guid? ContentId { get; set; }
    public bool IncludeChildren { get; set; }
    public string? LanguageIsoCode { get; set; }
}