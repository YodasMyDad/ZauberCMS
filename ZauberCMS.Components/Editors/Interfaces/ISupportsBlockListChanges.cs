using ZauberCMS.Core.Content.Models;

namespace ZauberCMS.Components.Editors.Interfaces;

/// <summary>
/// Marker interface implemented by property editors that bubble nested-block changes upward via a
/// <c>ChangesPending</c> action. <see cref="DynamicContentProperty"/> inspects this rather than
/// matching on the editor class name (which would silently break if the class were renamed).
/// </summary>
public interface ISupportsBlockListChanges
{
    Action<BlockListChanges>? ChangesPending { get; set; }
}
