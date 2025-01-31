using Microsoft.AspNetCore.Mvc.Rendering;
using ZauberCMS.Core.Content.Models;

namespace ZauberCMS.Routing.Views.Shared.Components.RenderBlock;

public class RenderBlockModel
{
    public Content? Content { get; set; }
    public RenderMode RenderMode { get; set; } = RenderMode.Static;
}