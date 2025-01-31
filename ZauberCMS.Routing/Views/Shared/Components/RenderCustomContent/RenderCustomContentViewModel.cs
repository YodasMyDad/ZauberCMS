using ZauberCMS.Core.Content.Interfaces;
using ZauberCMS.Core.Content.Models;

namespace ZauberCMS.Routing.Views.Shared.Components.RenderCustomContent;

public class RenderCustomContentViewModel
{
    public List<string> Components { get; set; } = [];
    public Dictionary<string, ICustomContentComponent>? CustomContentComponents { get; set; } = [];
    public Content Content { get; set; } = null!;
}