using Microsoft.AspNetCore.Mvc;
using ZauberCMS.Core.Content.Models;

namespace ZauberCMS.Web.Views.Shared.Components.BlogPostPreview;

public class BlogPostPreviewViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(Content? post)
    {
        return View(post);
    }
}