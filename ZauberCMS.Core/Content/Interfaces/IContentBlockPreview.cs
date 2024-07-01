using ZauberCMS.Core.Content.Models;

namespace ZauberCMS.Core.Content.Interfaces;

public interface IContentBlockPreview : IContentBlock
{
    ContentType? ContentType { get; set; }
}