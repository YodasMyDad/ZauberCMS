using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Media.Models;
using ZauberCMS.Core.Rendering;

namespace ZauberCMS.Web.Shared.Models;

public class SharedViewModel(Content content) : ZauberPageViewModel(content)
{
    public Media? HeaderImage { get; set; }
}