namespace ZauberCMS.Core.Content.Interfaces;

/// <summary>
/// A block's view on the site, matched to a block by <see cref="IContentBlock.ContentTypeAlias"/> and rendered with
/// <c>&lt;RenderBlock TView="IContentBlockView" /&gt;</c>. A block that needs interactivity sets its own
/// <c>@rendermode</c>.
/// </summary>
public interface IContentBlockView : IContentBlock
{
}
