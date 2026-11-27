namespace ZauberCMS.Core.Content.Interfaces;

/// <summary>
/// A component an editor can put on a page through the custom component picker; the page's view renders the chosen
/// ones with <c>RenderCustomContent</c>. Discovered; the component is given the id of the page it is on, not the
/// page itself: a component with <c>@rendermode</c> gets its parameters serialised into the page, and the whole
/// page with its values, type and relations does not belong there (ZA-126, Roadmap D58). Load what it needs with
/// <c>IContentService.GetContentAsync(new GetContentParameters { Id = ContentId, Cached = true })</c>.
/// </summary>
public interface ICustomContentComponent
{
    /// <summary>The name the picker shows.</summary>
    string Name { get; }

    /// <summary>The id of the page the component is on (declare it as a <c>[Parameter]</c>).</summary>
    Guid ContentId { get; set; }
}
