using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Media.Models;
using ZauberCMS.Core.Rendering;

namespace ZauberCMS.Web.Home.Models;

public class HomeViewModel(Content content) : ZauberPageViewModel(content)
{
    public Media? HeaderImage { get; set; }
    public List<Content> BlogPosts { get; set; } = [];
}