namespace ZauberCMS.Core.Content.Parameters;

public class GetContentFromRequestParameters
{
    public string? Slug { get; set; }
    public bool IsRootContent { get; set; }
    public bool IncludeChildren { get; set; }
    public bool IgnoreInternalRedirect { get; set; }
    public string? Url { get; set; }
}