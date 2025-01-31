namespace ZauberCMS.Core.Content.Interfaces;
using RenderMode = Microsoft.AspNetCore.Mvc.Rendering.RenderMode;

public interface IContentBlockView : IContentBlock
{
    RenderMode RenderMode { get; }
}