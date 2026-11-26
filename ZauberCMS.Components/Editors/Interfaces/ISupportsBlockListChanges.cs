using ZauberCMS.Core.Content.Models;

namespace ZauberCMS.Components.Editors.Interfaces;

/// <summary>
/// No longer used: nested block changes are not passed up through callbacks any more. A property editor that hosts
/// nested content takes the <see cref="Models.ContentEditScope"/> cascading parameter and reports its blocks to Core
/// with an <c>INestedContentReferenceProvider</c> (Docs/BlockListEditor.md).
/// </summary>
[Obsolete("Nested block changes go into the cascading ContentEditScope; ChangesPending is never set. See Docs/BlockListEditor.md.")]
public interface ISupportsBlockListChanges
{
#pragma warning disable CS0618 // Type or member is obsolete
    Action<BlockListChanges>? ChangesPending { get; set; }
#pragma warning restore CS0618
}
