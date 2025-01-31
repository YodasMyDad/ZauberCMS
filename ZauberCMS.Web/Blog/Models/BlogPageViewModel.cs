using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Media.Models;
using ZauberCMS.Core.Rendering;

namespace ZauberCMS.Web.Blog.Models;

public class BlogPageViewModel(Content content) : ZauberPageViewModel(content)
{
    public List<Content> ContentBlocks { get; set; } = content.GetValue<List<Content>>("Content") ?? [];
    public Media? HeaderImage { get; set; }
}