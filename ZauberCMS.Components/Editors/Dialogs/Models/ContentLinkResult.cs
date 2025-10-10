using ZauberCMS.Core.Content.Models;

namespace ZauberCMS.Components.Editors.Dialogs.Models;

public class ContentLinkResult
{
    public required Content Content { get; set; }
    public required string LinkText { get; set; }
    public required string Url { get; set; }
    public bool IsContentLink => true;
}

public class ManualLinkResult
{
    public required string Url { get; set; }
    public required string Text { get; set; }
    public string? Title { get; set; }
    public string? Target { get; set; }
    public string? Rel { get; set; }
    public Guid? ContentId { get; set; } // If link originated from content selection
    public bool IsContentLink => false;
}

