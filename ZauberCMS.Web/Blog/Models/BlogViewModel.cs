using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Media.Models;
using ZauberCMS.Core.Rendering;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Web.Blog.Models;

public class BlogViewModel(Content content) : ZauberPageViewModel(content)
{
    public Media? HeaderImage { get; set; }
    public PaginatedList<Content> BlogPosts { get; set; } = new();
}